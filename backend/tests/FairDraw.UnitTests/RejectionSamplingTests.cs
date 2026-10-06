using System.Security.Cryptography;
using System.Text;
using FairDraw.Crypto;

namespace FairDraw.UnitTests;

public class RejectionSamplingTests
{
    private static readonly GoldenVectors.VectorsDocument V = GoldenVectors.Data;

    [Fact]
    public void SelectWinnerIndex_MatchesGoldenVector_NoRejectionNeeded()
    {
        var (winnerIndex, attemptsUsed) = RejectionSampling.SelectWinnerIndex(V.ResultHash, V.OptionCount);

        Assert.Equal(V.WinnerIndex, winnerIndex);
        Assert.Equal(V.AttemptsUsed, attemptsUsed);
    }

    [Fact]
    public void SelectWinnerIndex_WithN3_TheOnlyBiasedValueIsUlongMaxValue()
    {
        // For N=3, limit = floor(2^64/3)*3 = 2^64 - 1 = ulong.MaxValue, so the *only* rejected
        // 8-byte prefix in the entire 2^64 space is 0xFFFFFFFFFFFFFFFF. This lets us force the
        // rejection branch deterministically without needing to search for a real SHA-256 preimage.
        var callCount = 0;
        byte[] ForcedHash(string seed)
        {
            callCount++;
            if (seed.EndsWith("|0", StringComparison.Ordinal))
            {
                // First 8 bytes = 0xFF...FF (the single rejected value for N=3), rest arbitrary.
                var rejected = new byte[32];
                for (var i = 0; i < 8; i++) rejected[i] = 0xFF;
                return rejected;
            }

            // Any accepted value; use a real hash so it's clearly not hand-picked to cheat the test.
            return SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        }

        var (winnerIndex, attemptsUsed) = RejectionSampling.SelectWinnerIndex("forced-rejection-vector", 3, ForcedHash);

        Assert.Equal(1, attemptsUsed);
        Assert.InRange(winnerIndex, 0, 2);
        Assert.Equal(2, callCount); // attempt 0 (rejected) + attempt 1 (accepted)
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(65536)]
    public void SelectWinnerIndex_PowerOfTwoOptionCount_NeverRejects(int optionCount)
    {
        // When N divides 2^64 evenly (true for every power of two), limit == 2^64 exactly,
        // and since the maximum representable value is 2^64 - 1, no value can ever be rejected.
        for (var i = 0; i < 25; i++)
        {
            var resultHash = CanonicalFormats.Sha256Hex($"probe-{i}");
            var (_, attemptsUsed) = RejectionSampling.SelectWinnerIndex(resultHash, optionCount);
            Assert.Equal(0, attemptsUsed);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(19)]
    public void SelectWinnerIndex_AlwaysWithinBounds(int optionCount)
    {
        for (var i = 0; i < 50; i++)
        {
            var resultHash = CanonicalFormats.Sha256Hex($"bounds-probe-{optionCount}-{i}");
            var (winnerIndex, _) = RejectionSampling.SelectWinnerIndex(resultHash, optionCount);
            Assert.InRange(winnerIndex, 0, optionCount - 1);
        }
    }

    [Fact]
    public void SelectWinnerIndex_IsDeterministic_SameInputsSameOutput()
    {
        var a = RejectionSampling.SelectWinnerIndex(V.ResultHash, V.OptionCount);
        var b = RejectionSampling.SelectWinnerIndex(V.ResultHash, V.OptionCount);

        Assert.Equal(a, b);
    }

    [Fact]
    public void SelectWinnerIndex_ChangingAnyRevealedNumber_CanChangeTheWinner()
    {
        // Not a proof for all cases (that's inherent to hashing), but demonstrates the property
        // required by the spec: the result depends on every participant, not just a sum.
        var original = V.Participants.Select(p => (p.ParticipantId, p.Number)).ToList();
        var tampered = original.ToList();
        tampered[0] = (tampered[0].ParticipantId, tampered[0].Number == 10 ? 1 : tampered[0].Number + 1);

        var originalHash = CanonicalFormats.Sha256Hex(CanonicalFormats.BuildRevealedDataCanonical(V.DrawId, original));
        var tamperedHash = CanonicalFormats.Sha256Hex(CanonicalFormats.BuildRevealedDataCanonical(V.DrawId, tampered));

        Assert.NotEqual(originalHash, tamperedHash);
    }

    [Fact]
    public void SelectWinnerIndex_RejectsNonPositiveOptionCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RejectionSampling.SelectWinnerIndex(V.ResultHash, 0));
    }
}
