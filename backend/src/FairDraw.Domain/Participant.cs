namespace FairDraw.Domain;

/// <summary>
/// A named slot in a draw. Identity is bound lazily via the "claim" mechanism:
/// whoever first claims this name (while the draw is Open/Committing) receives a
/// one-time secret whose SHA-256 hash is stored here as <see cref="ClaimSecretHash"/>.
/// The claim is a field on Participant (not a separate table) because it is strictly
/// 1:1, never revoked/re-issued, and is checked on every commit/reveal request (hot path).
/// </summary>
public class Participant
{
    public Guid Id { get; set; }
    public Guid DrawId { get; set; }
    public required string Name { get; set; }

    /// <summary>Deterministic 0-based ordering, fixed at creation/edit time and frozen at Lock.</summary>
    public int SortOrder { get; set; }

    /// <summary>SHA-256 (hex, lowercase) of the claim secret. Null until claimed.</summary>
    public string? ClaimSecretHash { get; set; }

    public DateTimeOffset? ClaimedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Draw? Draw { get; set; }
    public Commitment? Commitment { get; set; }
    public Reveal? Reveal { get; set; }

    public bool IsClaimed => ClaimSecretHash is not null;
}
