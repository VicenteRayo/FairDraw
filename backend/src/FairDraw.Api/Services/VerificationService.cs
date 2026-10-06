using FairDraw.Api.Dtos;
using FairDraw.Domain;
using FairDraw.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FairDraw.Api.Services;

/// <summary>
/// Builds the flat, stable payload consumed by the standalone /draw/{code}/verify page. Same
/// visibility rule as DrawViewMapper: reveals (number/secret) are only included once the draw is
/// Revealing/Completed, and only the valid ones.
/// </summary>
public class VerificationService(IDbContextFactory<FairDrawDbContext> dbFactory, TimeProvider clock)
{
    public async Task<VerificationResponse?> BuildAsync(string drawCode, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var draw = await db.Draws
            .Include(d => d.Options)
            .Include(d => d.Participants)
            .Include(d => d.Result).ThenInclude(r => r!.WinnerOption)
            .FirstOrDefaultAsync(d => d.PublicCode == drawCode, ct);

        if (draw is null || draw.IsExpired(clock.GetUtcNow()))
        {
            return null;
        }

        var commitments = await db.Commitments
            .Where(c => c.DrawId == draw.Id)
            .Select(c => new VerificationCommitmentDto(c.ParticipantId.ToString(), c.CommitmentHash, c.CommittedAt))
            .ToListAsync(ct);

        var revealsVisible = draw.State is DrawState.Revealing or DrawState.Completed;
        var reveals = revealsVisible
            ? await db.Reveals
                .Where(r => r.DrawId == draw.Id && r.IsValid)
                .Select(r => new VerificationRevealDto(r.ParticipantId.ToString(), r.Number!.Value, r.SecretHex!, r.RevealedAt))
                .ToListAsync(ct)
            : [];

        var options = draw.Options.OrderBy(o => o.SortOrder)
            .Select(o => new OptionDto(o.Id.ToString(), o.Text, o.SortOrder))
            .ToList();

        var participants = draw.Participants.OrderBy(p => p.SortOrder)
            .Select(p => new VerificationParticipantDto(p.Id.ToString(), p.Name, p.SortOrder))
            .ToList();

        ResultDto? result = draw.Result is null
            ? null
            : new ResultDto(
                draw.Result.ResultHash,
                draw.Result.AttemptsUsed,
                draw.Result.WinnerIndex,
                draw.Result.WinnerOptionId.ToString(),
                draw.Result.WinnerOption?.Text);

        return new VerificationResponse(
            ProtocolVersion: 1,
            DrawId: draw.PublicCode,
            State: draw.State.ToString(),
            ConfigurationHash: draw.ConfigurationHash,
            Options: options,
            Participants: participants,
            Commitments: commitments,
            Reveals: reveals,
            Result: result,
            ExpiresAt: draw.ExpiresAt);
    }
}
