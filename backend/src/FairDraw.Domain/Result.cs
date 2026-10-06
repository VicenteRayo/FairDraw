namespace FairDraw.Domain;

/// <summary>
/// The outcome of a completed draw. Exactly one per draw, computed once, when the last valid
/// reveal arrives. Fully reproducible from public data alone (see FairDraw.Crypto).
/// </summary>
public class Result
{
    public Guid Id { get; set; }
    public Guid DrawId { get; set; }

    /// <summary>SHA-256 (hex, lowercase) of the canonical revealed-data string ("resultHash" in the spec).</summary>
    public required string ResultHash { get; set; }

    /// <summary>How many rejection-sampling iterations were needed to avoid modulo bias (almost always 0).</summary>
    public int AttemptsUsed { get; set; }

    public int WinnerIndex { get; set; }
    public Guid WinnerOptionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Draw? Draw { get; set; }
    public Option? WinnerOption { get; set; }
}
