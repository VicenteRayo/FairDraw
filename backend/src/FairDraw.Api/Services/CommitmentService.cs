using System.Data;
using FairDraw.Api.Errors;
using FairDraw.Crypto;
using FairDraw.Domain;
using FairDraw.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FairDraw.Api.Services;

public record CommitResult(Guid ParticipantId, DateTimeOffset CommittedAt);

/// <summary>
/// Records a participant's commitment and, when it is the last one, resolves the draw.
///
/// The client sends its number and secret together with the commitment hash. The server checks
/// that they match the hash and stores them as a valid Reveal straight away, but never exposes
/// them until the draw has left the commit phase (see DrawViewMapper/VerificationService). When
/// the last commitment arrives and every participant already has a valid reveal, the Result is
/// computed in the same transaction and the draw goes directly to Completed — nobody has to
/// come back to reveal. Only if some commitment arrived without its reveal (an older client)
/// does the draw fall back to Revealing and wait for the missing reveals.
///
/// Why the transaction matters: this is the classic "who commits last" race: under plain Read
/// Committed isolation, the two final participants committing at the same instant could each
/// count the OTHER's (not-yet-committed) row as missing and neither would trigger the
/// transition, leaving the draw stuck at N-1/N forever. Wrapping each attempt in a fresh,
/// Serializable transaction turns that race into a detected write conflict on retry, so the
/// retried attempt re-reads the now-complete state and transitions correctly exactly once.
/// </summary>
public class CommitmentService(IDbContextFactory<FairDrawDbContext> dbFactory, TimeProvider clock)
{
    public async Task<CommitResult> CommitAsync(
        string drawCode, Guid participantId, string? participantSecret, string commitmentHash,
        int? number, string? secretHex, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            IDbContextTransaction? tx = null;

            try
            {
                // BeginTransactionAsync itself can throw under contention (e.g. SQLite SQLITE_BUSY
                // acquiring the write lock for a Serializable transaction), so it must be inside
                // the try, not before it, or that failure would bypass the retry loop entirely.
                tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var draw = (await db.Draws
                    .Include(d => d.Options)
                    .Include(d => d.Participants)
                    .FirstOrDefaultAsync(d => d.PublicCode == drawCode, ct)).RequireNotExpired(clock);

                var participant = draw.Participants.FirstOrDefault(p => p.Id == participantId)
                    ?? throw ApiErrors.NotFound("participant_not_found");

                CredentialAuthenticator.Verify(participantSecret, participant.ClaimSecretHash, "participant_secret_required", "invalid_participant_secret");

                if (draw.State is not (DrawState.Open or DrawState.Committing))
                {
                    throw ApiErrors.Conflict("draw_not_accepting_commitments");
                }

                if (!IsValidSha256Hex(commitmentHash))
                {
                    throw ApiErrors.Validation("invalid_commitment_format");
                }

                var normalizedCommitment = commitmentHash.ToLowerInvariant();
                string? normalizedSecret = null;
                if (number is not null || secretHex is not null)
                {
                    if (number is not (>= 1 and <= 10))
                    {
                        throw ApiErrors.Validation("number_out_of_range");
                    }

                    if (secretHex is null || !RevealService.IsValidSecretHex(secretHex))
                    {
                        throw ApiErrors.Validation("invalid_secret_format");
                    }

                    normalizedSecret = secretHex.ToLowerInvariant();
                    var canonical = CanonicalFormats.BuildCommitmentCanonical(draw.PublicCode, participantId.ToString(), number.Value, normalizedSecret);
                    if (!string.Equals(CanonicalFormats.Sha256Hex(canonical), normalizedCommitment, StringComparison.Ordinal))
                    {
                        // Nothing is stored: a commitment whose attached reveal doesn't match it
                        // would otherwise leave the draw needing a separate reveal round.
                        throw ApiErrors.Unprocessable("reveal_does_not_match_commitment");
                    }
                }

                var alreadyCommitted = await db.Commitments.AnyAsync(c => c.ParticipantId == participantId, ct);
                if (alreadyCommitted)
                {
                    throw ApiErrors.Conflict("commitment_already_exists");
                }

                var now = clock.GetUtcNow();
                db.Commitments.Add(new Commitment
                {
                    Id = Guid.NewGuid(),
                    ParticipantId = participantId,
                    DrawId = draw.Id,
                    CommitmentHash = normalizedCommitment,
                    CommittedAt = now
                });

                if (normalizedSecret is not null)
                {
                    db.Reveals.Add(new Reveal
                    {
                        Id = Guid.NewGuid(),
                        ParticipantId = participantId,
                        DrawId = draw.Id,
                        Number = number,
                        SecretHex = normalizedSecret,
                        IsValid = true,
                        Attempts = 1,
                        RevealedAt = now
                    });
                }

                if (draw.State == DrawState.Open)
                {
                    draw.State = DrawState.Committing;
                }

                await db.SaveChangesAsync(ct);

                var totalParticipants = draw.Participants.Count;
                var totalCommitments = await db.Commitments.CountAsync(c => c.DrawId == draw.Id, ct);
                if (totalCommitments == totalParticipants)
                {
                    var validRevealCount = await db.Reveals.CountAsync(r => r.DrawId == draw.Id && r.IsValid, ct);
                    if (validRevealCount == totalParticipants)
                    {
                        await RevealService.ComputeAndPersistResultAsync(db, draw, now, ct);
                    }
                    else
                    {
                        draw.State = DrawState.Revealing;
                        await db.SaveChangesAsync(ct);
                    }
                }

                await tx.CommitAsync(ct);
                return new CommitResult(participantId, now);
            }
            catch (Exception ex) when (attempt < TransactionHelpers.DefaultMaxAttempts && TransactionHelpers.IsTransientConflict(ex))
            {
                if (tx is not null) await tx.RollbackAsync(ct);
                await Task.Delay(Random.Shared.Next(10, 50), ct);
            }
            catch (DbUpdateException ex) when (TransactionHelpers.IsUniqueConstraintViolation(ex))
            {
                // True simultaneous double-commit slipped past the existence check above.
                if (tx is not null) await tx.RollbackAsync(ct);
                throw ApiErrors.Conflict("commitment_already_exists");
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

    private static bool IsValidSha256Hex(string value) =>
        value.Length == 64 && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
}
