using FairDraw.Domain;
using FairDraw.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairDraw.Api.Services;

/// <summary>
/// Physically deletes draws older than Draw.RetentionDays — separate from the "not expired" query
/// filter (DrawQueryExtensions) applied on every read/write, which is what actually guarantees the
/// exact 404-at-30-days boundary. This purger's job is just data hygiene: making sure abandoned or
/// completed draws don't linger forever in the database (participant names, revealed numbers,
/// secrets) once nobody can reach them through the API anyway. Run periodically by
/// DrawExpirationHostedService; kept as a plain, directly-testable class rather than bundled into
/// the BackgroundService itself.
/// </summary>
public class DrawExpirationPurger(IDbContextFactory<FairDrawDbContext> dbFactory, TimeProvider clock, ILogger<DrawExpirationPurger> logger)
{
    public async Task<int> PurgeExpiredDrawsAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var now = clock.GetUtcNow();

        // Expiry is decided client-side (see DrawQueryExtensions for why: the SQLite provider used
        // in tests cannot translate DateTimeOffset comparisons in a SQL WHERE clause, while Postgres
        // can — comparing in memory keeps this correct on both). Only Id/CreatedAt are projected, so
        // this stays cheap even with many draws. The actual bulk DELETE is then a plain Guid.Contains
        // filter, which both providers translate fine.
        var candidates = await db.Draws
            .Select(d => new { d.Id, d.CreatedAt })
            .ToListAsync(ct);

        var expiredIds = candidates
            .Where(d => now >= d.CreatedAt.AddDays(Draw.RetentionDays))
            .Select(d => d.Id)
            .ToList();

        if (expiredIds.Count == 0)
        {
            return 0;
        }

        // A bulk DELETE — bypasses the change tracker, but the ON DELETE CASCADE constraints
        // configured in FairDraw.Infrastructure (Options, Participants, Commitments, Reveals,
        // Result all cascade from Draw) are enforced by the database itself, so related rows are
        // removed too without needing to load the full entity graph first.
        var deleted = await db.Draws.Where(d => expiredIds.Contains(d.Id)).ExecuteDeleteAsync(ct);

        logger.LogInformation("Purged {Count} draw(s) older than {RetentionDays} days.", deleted, Draw.RetentionDays);

        return deleted;
    }
}
