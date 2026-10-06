using FairDraw.Crypto;

namespace FairDraw.UnitTests;

public class CanonicalFormatsTests
{
    private static readonly GoldenVectors.VectorsDocument V = GoldenVectors.Data;

    [Fact]
    public void BuildConfigurationCanonical_MatchesGoldenVector()
    {
        var canonical = CanonicalFormats.BuildConfigurationCanonical(V.DrawId, V.Options, V.ParticipantsNames);

        Assert.Equal(V.ConfigurationCanonical, canonical);
    }

    [Fact]
    public void ConfigurationHash_MatchesGoldenVector()
    {
        var canonical = CanonicalFormats.BuildConfigurationCanonical(V.DrawId, V.Options, V.ParticipantsNames);

        var hash = CanonicalFormats.Sha256Hex(canonical);

        Assert.Equal(V.ConfigurationHash, hash);
    }

    [Theory]
    [MemberData(nameof(ParticipantIndexes))]
    public void BuildCommitmentCanonical_MatchesGoldenVector(int index)
    {
        var p = V.Participants[index];

        var canonical = CanonicalFormats.BuildCommitmentCanonical(V.DrawId, p.ParticipantId, p.Number, p.SecretHex);

        Assert.Equal(p.CommitmentCanonical, canonical);
    }

    [Theory]
    [MemberData(nameof(ParticipantIndexes))]
    public void Commitment_MatchesGoldenVector(int index)
    {
        var p = V.Participants[index];

        var canonical = CanonicalFormats.BuildCommitmentCanonical(V.DrawId, p.ParticipantId, p.Number, p.SecretHex);
        var hash = CanonicalFormats.Sha256Hex(canonical);

        Assert.Equal(p.CommitmentHash, hash);
    }

    [Fact]
    public void BuildRevealedDataCanonical_MatchesGoldenVector()
    {
        var reveals = V.Participants.Select(p => (p.ParticipantId, p.Number)).ToList();

        var canonical = CanonicalFormats.BuildRevealedDataCanonical(V.DrawId, reveals);

        Assert.Equal(V.ResultCanonical, canonical);
    }

    [Fact]
    public void ResultHash_MatchesGoldenVector()
    {
        var reveals = V.Participants.Select(p => (p.ParticipantId, p.Number)).ToList();

        var canonical = CanonicalFormats.BuildRevealedDataCanonical(V.DrawId, reveals);
        var hash = CanonicalFormats.Sha256Hex(canonical);

        Assert.Equal(V.ResultHash, hash);
    }

    [Fact]
    public void BuildRevealedDataCanonical_IsInsensitiveToInputOrder()
    {
        var reveals = V.Participants.Select(p => (p.ParticipantId, p.Number)).ToList();
        var shuffled = reveals.AsEnumerable().Reverse().ToList();

        var canonicalFromOriginal = CanonicalFormats.BuildRevealedDataCanonical(V.DrawId, reveals);
        var canonicalFromShuffled = CanonicalFormats.BuildRevealedDataCanonical(V.DrawId, shuffled);

        Assert.Equal(canonicalFromOriginal, canonicalFromShuffled);
    }

    [Theory]
    [InlineData("Roma", true)]
    [InlineData("Vicente|Juan", false)]
    [InlineData("", true)]
    public void IsSafeForCanonical_RejectsPipeCharacter(string value, bool expectedSafe)
    {
        Assert.Equal(expectedSafe, CanonicalFormats.IsSafeForCanonical(value));
    }

    public static IEnumerable<object[]> ParticipantIndexes()
    {
        for (var i = 0; i < V.Participants.Count; i++)
        {
            yield return [i];
        }
    }
}
