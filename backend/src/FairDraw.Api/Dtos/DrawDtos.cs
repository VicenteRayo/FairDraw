namespace FairDraw.Api.Dtos;

public record CreateDrawRequest(string Name, List<string> Options, List<string> Participants);

public record UpdateDrawRequest(string Name, List<string> Options, List<string> Participants);

public record OptionDto(string Id, string Text, int SortOrder);

public record ParticipantDto(string Id, string Name, int SortOrder, bool Claimed, bool HasCommitted, RevealSummaryDto? Reveal);

public record RevealSummaryDto(int Number, string SecretHex, DateTimeOffset RevealedAt);

public record ResultDto(string ResultHash, int AttemptsUsed, int WinnerIndex, string WinnerOptionId, string? WinnerOptionText);

/// <summary>UI-facing view of a draw. Never includes a number/secret unless the draw is Revealing/Completed
/// and that specific participant's reveal is valid (see DrawViewService.MapToDto).</summary>
public record DrawDto(
    string DrawId,
    string Name,
    string State,
    string? ConfigurationHash,
    List<OptionDto> Options,
    List<ParticipantDto> Participants,
    int CommitCount,
    int ParticipantCount,
    ResultDto? Result,
    DateTimeOffset ExpiresAt);

public record CreateDrawResponse(string DrawId, string State, string CreatorToken, DrawDto Draw);

public record ClaimResponse(string ParticipantId, string ParticipantSecret, DateTimeOffset ClaimedAt);

/// <summary>
/// Number and Secret are sent together with the commitment so the server can resolve the draw
/// as soon as the last participant commits, with no separate reveal round. They are optional only
/// so that a client that commits the hash alone still works (the draw then falls back to the
/// Revealing phase, which requires every participant to come back and reveal).
/// </summary>
public record CommitRequest(string Commitment, int? Number = null, string? Secret = null);

public record CommitResponse(string ParticipantId, DateTimeOffset CommittedAt);

public record RevealRequest(int Number, string Secret);

public record RevealResponse(string ParticipantId, bool Valid, DateTimeOffset RevealedAt);

public record ErrorResponse(string Error);

// --- /verification: a flat, stable payload for independent client-side recomputation. ---

public record VerificationCommitmentDto(string ParticipantId, string CommitmentHash, DateTimeOffset CommittedAt);

public record VerificationRevealDto(string ParticipantId, int Number, string SecretHex, DateTimeOffset RevealedAt);

public record VerificationParticipantDto(string Id, string Name, int SortOrder);

public record VerificationResponse(
    int ProtocolVersion,
    string DrawId,
    string State,
    string? ConfigurationHash,
    List<OptionDto> Options,
    List<VerificationParticipantDto> Participants,
    List<VerificationCommitmentDto> Commitments,
    List<VerificationRevealDto> Reveals,
    ResultDto? Result,
    DateTimeOffset ExpiresAt);
