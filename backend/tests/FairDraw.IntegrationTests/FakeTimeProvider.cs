namespace FairDraw.IntegrationTests;

/// <summary>Minimal controllable clock so expiration tests can jump forward in time without waiting.</summary>
public class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan span) => _now += span;
}
