namespace FairDraw.Domain;

/// <summary>
/// The reveal half of the Commit-Reveal protocol. Exactly one row per participant, but it may
/// be overwritten while <see cref="IsValid"/> is false: since the commitment is already immutable,
/// a retry with different data simply keeps failing the hash check, so allowing retries does not
/// weaken the protocol. Rows with IsValid=false are never exposed on public/unauthenticated endpoints.
/// </summary>
public class Reveal
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid DrawId { get; set; }

    public int? Number { get; set; }

    /// <summary>32 random bytes, hex-encoded lowercase (64 chars).</summary>
    public string? SecretHex { get; set; }

    /// <summary>True only if SHA-256(canonical reveal string) matches the stored commitment hash.</summary>
    public bool IsValid { get; set; }

    /// <summary>Number of reveal submissions made for this participant (valid or not), for observability.</summary>
    public int Attempts { get; set; }

    public DateTimeOffset RevealedAt { get; set; }

    public Participant? Participant { get; set; }
}
