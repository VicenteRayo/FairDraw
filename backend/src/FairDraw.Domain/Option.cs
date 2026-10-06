namespace FairDraw.Domain;

/// <summary>One of the choices a draw can resolve to (e.g. "Roma").</summary>
public class Option
{
    public Guid Id { get; set; }
    public Guid DrawId { get; set; }
    public required string Text { get; set; }

    /// <summary>Deterministic 0-based ordering, fixed at creation/edit time and frozen at Lock.</summary>
    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Draw? Draw { get; set; }
}
