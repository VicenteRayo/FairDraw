using System.Text.RegularExpressions;
using FairDraw.Crypto;

namespace FairDraw.UnitTests;

public partial class SecretGeneratorTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(1)]
    [InlineData(16)]
    public void GenerateHex_ReturnsLowercaseHexOfExpectedLength(int byteLength)
    {
        var hex = SecretGenerator.GenerateHex(byteLength);

        Assert.Equal(byteLength * 2, hex.Length);
        Assert.Matches(LowercaseHexRegex(), hex);
    }

    [Fact]
    public void GenerateHex_ProducesDifferentValuesEachTime()
    {
        var values = Enumerable.Range(0, 20).Select(_ => SecretGenerator.GenerateHex(32)).ToHashSet();

        Assert.Equal(20, values.Count);
    }

    [Fact]
    public void GenerateNumberInRange_AlwaysWithinBounds()
    {
        for (var i = 0; i < 500; i++)
        {
            var n = SecretGenerator.GenerateNumberInRange(1, 10);
            Assert.InRange(n, 1, 10);
        }
    }

    [Fact]
    public void GenerateNumberInRange_CoversFullRangeOverManySamples()
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < 2000; i++)
        {
            seen.Add(SecretGenerator.GenerateNumberInRange(1, 10));
        }

        Assert.Equal(Enumerable.Range(1, 10).ToHashSet(), seen);
    }

    [Fact]
    public void GenerateNumberInRange_RejectsRangeLargerThan256()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SecretGenerator.GenerateNumberInRange(1, 300));
    }

    [GeneratedRegex("^[0-9a-f]+$")]
    private static partial Regex LowercaseHexRegex();
}
