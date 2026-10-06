namespace FairDraw.Domain;

/// <summary>
/// A single draw (sorteo). <see cref="Id"/> is the internal database key and must
/// never appear in canonical hashing material or public URLs — <see cref="PublicCode"/>
/// is the "drawId" used everywhere in the cryptographic protocol and in routing.
/// </summary>
public class Draw
{
    /// <summary>
    /// A draw's data — including its result if it has one — is only ever available for this many
    /// days after creation, whether it completed, was cancelled, or was simply abandoned half-open.
    /// After this, it is treated as not found by the API (see the expiry check in each Api service)
    /// and is physically deleted by a periodic background sweep (DrawExpirationHostedService).
    /// </summary>
    public const int RetentionDays = 30;

    public Guid Id { get; set; }

    /// <summary>Short public routing code, e.g. "8F4K92LM". Used verbatim as "drawId" in every canonical string.</summary>
    public required string PublicCode { get; set; }

    public required string Name { get; set; }

    public DrawState State { get; set; } = DrawState.Created;

    /// <summary>SHA-256 (hex, lowercase) of the canonical configuration string. Set once, at Lock time.</summary>
    public string? ConfigurationHash { get; set; }

    /// <summary>SHA-256 (hex, lowercase) of the creator's bearer token. The raw token is never stored.</summary>
    public required string CreatorTokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public List<Option> Options { get; set; } = [];
    public List<Participant> Participants { get; set; } = [];
    public Result? Result { get; set; }

    public DateTimeOffset ExpiresAt => CreatedAt.AddDays(RetentionDays);

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
