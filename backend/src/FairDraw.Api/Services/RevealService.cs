using System.Data;
using FairDraw.Api.Errors;
using FairDraw.Crypto;
using FairDraw.Domain;
using FairDraw.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FairDraw.Api.Services;

public record RevealResult(Guid ParticipantId, bool Valid, DateTimeOffset RevealedAt);

/// <summary>
/// Validates a participant's reveal against their stored commitment and, once every participant
/// has a valid reveal, computes and persists the Result. Wrapped the same way as
/// <see cref="CommitmentService"/> and for the same reason: the last two reveals arriving at the
/// same instant must not both fail to notice completion (or worse, both attempt to write the
/// Result row).
/// </summary>
public class RevealService(IDbContextFactory<FairDrawDbContext> dbFactory, TimeProvider clock)
{
    public async Task<RevealResult> RevealAsync(
        string drawCode, Guid participantId, string? participantSecret, int number, string secretHex, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            IDbContextTransaction? tx = null;

            try
            {
                // See CommitmentService for why BeginTransactionAsync must be inside the try.
                tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var draw = (await db.Draws
                    .Include(d => d.Options)
                    .Include(d => d.Participants).ThenInclude(p => p.Commitment)
                    .FirstOrDefaultAsync(d => d.PublicCode == drawCode, ct)).RequireNotExpired(clock);

                var participant = draw.Participants.FirstOrDefault(p => p.Id == participantId)
                    ?? throw ApiErrors.NotFound("participant_not_found");

                CredentialAuthenticator.Verify(participantSecret, participant.ClaimSecretHash, "participant_secret_required", "invalid_participant_secret");

                if (draw.State != DrawState.Revealing)
                {
                    throw ApiErrors.Conflict("draw_not_revealing");
                }

                if (participant.Commitment is null)
                {
                    // Cannot happen through normal flow (Revealing implies 100% committed), but
                    // guarded explicitly rather than assumed.
                    throw ApiErrors.Conflict("no_commitment_to_reveal");
                }

                if (number is < 1 or > 10)
                {
                    throw ApiErrors.Validation("number_out_of_range");
                }

                if (!IsValidSecretHex(secretHex))
                {
                    throw ApiErrors.Validation("invalid_secret_format");
                }

                var existingReveal = await db.Reveals.FirstOrDefaultAsync(r => r.ParticipantId == participantId, ct);
                if (existingReveal is { IsValid: true })
                {
                    throw ApiErrors.Conflict("reveal_already_valid");
                }

                var normalizedSecret = secretHex.ToLowerInvariant();
                var canonical = CanonicalFormats.BuildCommitmentCanonical(draw.PublicCode, participantId.ToString(), number, normalizedSecret);
                var recomputedHash = CanonicalFormats.Sha256Hex(canonical);
                var isValid = string.Equals(recomputedHash, participant.Commitment.CommitmentHash, StringComparison.Ordinal);
                var now = clock.GetUtcNow();

                if (existingReveal is null)
                {
                    existingReveal = new Reveal { Id = Guid.NewGuid(), ParticipantId = participantId, DrawId = draw.Id };
                    db.Reveals.Add(existingReveal);
                }

                existingReveal.Attempts += 1;
                existingReveal.RevealedAt = now;
                existingReveal.IsValid = isValid;
                // Only ever persist number/secret once verified valid — an invalid attempt is
                // recorded (Attempts, IsValid=false) but its payload is discarded, so a wrong
                // guess is never at risk of being exposed on any public endpoint.
                existingReveal.Number = isValid ? number : null;
                existingReveal.SecretHex = isValid ? normalizedSecret : null;

                await db.SaveChangesAsync(ct);

                if (isValid)
                {
                    var totalParticipants = draw.Participants.Count;
                    var validRevealCount = await db.Reveals.CountAsync(r => r.DrawId == draw.Id && r.IsValid, ct);

                    if (validRevealCount == totalParticipants)
                    {
                        await ComputeAndPersistResultAsync(db, draw, now, ct);
                    }
                }

                await tx.CommitAsync(ct);
                return new RevealResult(participantId, isValid, now);
            }
            catch (Exception ex) when (attempt < TransactionHelpers.DefaultMaxAttempts && TransactionHelpers.IsTransientConflict(ex))
            {
                if (tx is not null) await tx.RollbackAsync(ct);
                await Task.Delay(Random.Shared.Next(10, 50), ct);
            }
            catch (DbUpdateException ex) when (TransactionHelpers.IsUniqueConstraintViolation(ex))
            {
                if (tx is not null) await tx.RollbackAsync(ct);
                throw ApiErrors.Conflict("reveal_conflict");
            }
            catch
            {
                if (tx is not null) await tx.RollbackAsync(ct);
                throw;
            }
            finally
            {
                if (tx is not null) await tx.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Computes the Result from every valid reveal and moves the draw to Completed. Callers must
    /// have loaded <c>draw.Options</c> and must run inside the same Serializable transaction that
    /// established that all reveals are valid. Shared with <see cref="CommitmentService"/>, which
    /// completes the draw directly when the last commitment arrives with its reveal attached.
    /// </summary>
    internal static async Task ComputeAndPersistResultAsync(FairDrawDbContext db, Draw draw, DateTimeOffset now, CancellationToken ct)
    {
        var validReveals = await db.Reveals
            .Where(r => r.DrawId == draw.Id && r.IsValid)
            .Select(r => new { r.ParticipantId, Number = r.Number!.Value })
            .ToListAsync(ct);

        var revealTuples = validReveals.Select(r => (r.ParticipantId.ToString(), r.Number)).ToList();
        var canonicalRevealed = CanonicalFormats.BuildRevealedDataCanonical(draw.PublicCode, revealTuples);
        var resultHash = CanonicalFormats.Sha256Hex(canonicalRevealed);

        var orderedOptions = draw.Options.OrderBy(o => o.SortOrder).ToList();
        var (winnerIndex, attemptsUsed) = RejectionSampling.SelectWinnerIndex(resultHash, orderedOptions.Count);
        var winnerOption = orderedOptions[winnerIndex];

        db.Results.Add(new Result
        {
            Id = Guid.NewGuid(),
            DrawId = draw.Id,
            ResultHash = resultHash,
            AttemptsUsed = attemptsUsed,
            WinnerIndex = winnerIndex,
            WinnerOptionId = winnerOption.Id,
            CreatedAt = now
        });

        draw.State = DrawState.Completed;
        draw.CompletedAt = now;

        await db.SaveChangesAsync(ct);
    }

    internal static bool IsValidSecretHex(string value) =>
        value.Length == 64 && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
}
