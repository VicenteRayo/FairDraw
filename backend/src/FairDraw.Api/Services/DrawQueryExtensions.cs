using FairDraw.Api.Errors;
using FairDraw.Domain;

namespace FairDraw.Api.Services;

/// <summary>
/// Applied everywhere a Draw is looked up by its public code, so an expired draw (see
/// Draw.RetentionDays) is treated exactly like one that never existed — a 404, not a special
/// "expired" status — regardless of whether it completed, was cancelled, or was simply abandoned
/// half-open. Physical deletion of expired rows happens separately and asynchronously via
/// DrawExpirationHostedService; this is what makes the 404 boundary exact and immediate at the
/// 30-day mark even if the periodic sweep hasn't run yet.
///
/// Deliberately a post-fetch, in-memory check rather than a `WHERE CreatedAt > @cutoff` SQL filter:
/// the SQLite provider (used in integration tests) cannot translate DateTimeOffset comparisons
/// ("could not be translated" at query-compile time), while Postgres can — filtering client-side
/// keeps the exact same code path correct on both, and is cheap since this is always a single-row
/// lookup by unique code, never a large collection scan.
/// </summary>
public static class DrawQueryExtensions
{
    public static Draw RequireNotExpired(this Draw? draw, TimeProvider clock)
    {
        if (draw is null || draw.IsExpired(clock.GetUtcNow()))
        {
            throw ApiErrors.NotFound("draw_not_found");
        }

        return draw;
    }
}
