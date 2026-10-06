using FairDraw.Api.Dtos;
using FairDraw.Domain;

namespace FairDraw.Api.Services;

/// <summary>
/// Maps a fully-loaded Draw entity to its public DTO. Hard rule enforced here: a participant's
/// number/secret is only ever included once the draw has reached Revealing/Completed AND that
/// specific reveal is valid — never before, and never for an invalid attempt.
/// </summary>
public static class DrawViewMapper
{
    public static DrawDto ToDto(Draw draw)
    {
        var revealsVisible = draw.State is DrawState.Revealing or DrawState.Completed;

        var participants = draw.Participants
            .OrderBy(p => p.SortOrder)
            .Select(p => new ParticipantDto(
                Id: p.Id.ToString(),
                Name: p.Name,
                SortOrder: p.SortOrder,
                Claimed: p.IsClaimed,
                HasCommitted: p.Commitment is not null,
                Reveal: revealsVisible && p.Reveal is { IsValid: true }
                    ? new RevealSummaryDto(p.Reveal.Number!.Value, p.Reveal.SecretHex!, p.Reveal.RevealedAt)
                    : null))
            .ToList();

        var options = draw.Options.OrderBy(o => o.SortOrder)
            .Select(o => new OptionDto(o.Id.ToString(), o.Text, o.SortOrder))
            .ToList();

        ResultDto? result = draw.Result is null
            ? null
            : new ResultDto(
                draw.Result.ResultHash,
                draw.Result.AttemptsUsed,
                draw.Result.WinnerIndex,
                draw.Result.WinnerOptionId.ToString(),
                draw.Result.WinnerOption?.Text);

        return new DrawDto(
            DrawId: draw.PublicCode,
            Name: draw.Name,
            State: draw.State.ToString(),
            ConfigurationHash: draw.ConfigurationHash,
            Options: options,
            Participants: participants,
            CommitCount: draw.Participants.Count(p => p.Commitment is not null),
            ParticipantCount: draw.Participants.Count,
            Result: result,
            ExpiresAt: draw.ExpiresAt);
    }
}
