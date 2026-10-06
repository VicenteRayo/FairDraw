using FairDraw.Domain;

namespace FairDraw.UnitTests;

public class DrawExpirationTests
{
    private static Draw NewDraw(DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        PublicCode = "TESTCODE",
        Name = "Test",
        CreatorTokenHash = "hash",
        CreatedAt = createdAt
    };

    [Fact]
    public void ExpiresAt_IsExactlyRetentionDaysAfterCreation()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var draw = NewDraw(createdAt);

        Assert.Equal(createdAt.AddDays(30), draw.ExpiresAt);
        Assert.Equal(30, Draw.RetentionDays);
    }

    [Fact]
    public void IsExpired_FalseOneTickBeforeExpiry()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var draw = NewDraw(createdAt);

        var justBefore = draw.ExpiresAt - TimeSpan.FromTicks(1);

        Assert.False(draw.IsExpired(justBefore));
    }

    [Fact]
    public void IsExpired_TrueExactlyAtExpiry()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var draw = NewDraw(createdAt);

        Assert.True(draw.IsExpired(draw.ExpiresAt));
    }

    [Fact]
    public void IsExpired_TrueLongAfterExpiry()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var draw = NewDraw(createdAt);

        Assert.True(draw.IsExpired(draw.ExpiresAt.AddYears(1)));
    }

    [Fact]
    public void IsExpired_DoesNotDependOnDrawState()
    {
        // The 30-day expiry applies uniformly whether the draw completed, was cancelled, or was
        // simply abandoned half-open — IsExpired only ever looks at CreatedAt.
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var afterExpiry = createdAt.AddDays(31);

        foreach (var state in Enum.GetValues<DrawState>())
        {
            var draw = NewDraw(createdAt);
            draw.State = state;
            Assert.True(draw.IsExpired(afterExpiry));
        }
    }
}
