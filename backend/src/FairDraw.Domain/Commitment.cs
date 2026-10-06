namespace FairDraw.Domain;

/// <summary>
/// The commit half of the Commit-Reveal protocol. Exactly one per participant.
/// Only the hash is ever stored — the server never sees the number or secret at this stage.
/// Once created, a commitment can never be modified (enforced by the service layer, not just
/// convention: the API returns 409 for any attempt to commit twice for the same participant).
/// </summary>
public class Commitment
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid DrawId { get; set; }

    /// <summary>SHA-256 (hex, lowercase) of the canonical commitment string. See FairDraw.Crypto.CanonicalFormats.</summary>
    public required string CommitmentHash { get; set; }

    public DateTimeOffset CommittedAt { get; set; }

    public Participant? Participant { get; set; }
}
