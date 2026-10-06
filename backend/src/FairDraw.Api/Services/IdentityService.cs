using FairDraw.Api.Errors;
using FairDraw.Crypto;
using FairDraw.Domain;
using FairDraw.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FairDraw.Api.Services;

/// <summary>
/// "Claim your name" identity binding: the first browser to claim a participant's name receives a
/// one-time secret; every subsequent commit/reveal for that participant must present it.
/// </summary>
public class IdentityService(IDbContextFactory<FairDrawDbContext> dbFactory, TimeProvider clock)
{
    public async Task<(string ParticipantSecret, DateTimeOffset ClaimedAt)> ClaimAsync(
        string drawCode, Guid participantId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var draw = (await db.Draws.FirstOrDefaultAsync(d => d.PublicCode == drawCode, ct)).RequireNotExpired(clock);

        // Claiming is only meaningful once the participant list is frozen (Open) and while commits
        // are still in progress (Committing); it is intentionally disallowed in Created (list still
        // mutable) and moot afterwards (Revealing implies everyone already claimed to be able to commit).
        if (draw.State is not (DrawState.Open or DrawState.Committing))
        {
            throw ApiErrors.Conflict("draw_not_claimable");
        }

        var secret = SecretGenerator.GenerateHex(32);
        var hash = CanonicalFormats.Sha256Hex(secret);
        var now = clock.GetUtcNow();

        // Atomic conditional update ("claim only if still unclaimed") avoids a race where two
        // browsers claiming the same name at the same instant could otherwise both "succeed" and
        // overwrite each other's secret hash. Retried a few times against transient lock
        // contention (real under SQLite's single-writer model; theoretically possible, though much
        // less likely, under Postgres row locks too).
        var updated = await ExecuteUpdateWithRetryAsync(db, participantId, draw.Id, hash, now, ct);

        if (updated == 0)
        {
            var exists = await db.Participants.AnyAsync(p => p.Id == participantId && p.DrawId == draw.Id, ct);
            throw exists ? ApiErrors.Forbidden("already_claimed") : ApiErrors.NotFound("participant_not_found");
        }

        return (secret, now);
    }

    private static async Task<int> ExecuteUpdateWithRetryAsync(
        FairDrawDbContext db, Guid participantId, Guid drawId, string claimSecretHash, DateTimeOffset claimedAt, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await db.Participants
                    .Where(p => p.Id == participantId && p.DrawId == drawId && p.ClaimSecretHash == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.ClaimSecretHash, claimSecretHash)
                        .SetProperty(p => p.ClaimedAt, claimedAt), ct);
            }
            catch (Exception ex) when (attempt < TransactionHelpers.DefaultMaxAttempts && TransactionHelpers.IsTransientConflict(ex))
            {
                await Task.Delay(Random.Shared.Next(10, 50), ct);
            }
        }
    }
}
