using System.Net;
using FairDraw.Api.Dtos;
using FairDraw.Crypto;

namespace FairDraw.IntegrationTests;

/// <summary>
/// The default flow: each participant sends number + secret together with the commitment, and the
/// draw completes as soon as the last participant commits — no separate reveal round.
/// </summary>
public class CommitWithRevealTests(FairDrawApiFactory factory) : IClassFixture<FairDrawApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CommitWithReveal_LastOfAll_CompletesDrawImmediately()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B", "C"], ["Ana", "Bruno", "Carla"]);
        var numbers = new[] { 7, 3, 9 };
        var commits = participants.Select((p, i) => (p, DrawFlow.BuildCommitment(drawId, p.ParticipantId, numbers[i]))).ToList();

        foreach (var (p, c) in commits)
        {
            var response = await _client.CommitWithRevealAsync(drawId, p.ParticipantId, p.Secret, c.Commitment, c.Number, c.SecretHex);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var draw = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Completed", draw.State);
        Assert.NotNull(draw.Result);
        Assert.All(draw.Participants, p => Assert.NotNull(p.Reveal));

        // The published result must be exactly what the protocol yields from the public data.
        var expectedHash = CanonicalFormats.Sha256Hex(CanonicalFormats.BuildRevealedDataCanonical(
            drawId, commits.Select(x => (x.p.ParticipantId, x.Item2.Number)).ToList()));
        Assert.Equal(expectedHash, draw.Result!.ResultHash);
        var (expectedIndex, _) = RejectionSampling.SelectWinnerIndex(expectedHash, 3);
        Assert.Equal(expectedIndex, draw.Result.WinnerIndex);
    }

    [Fact]
    public async Task CommitWithReveal_BeforeLast_NeverExposesNumberOrSecret()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var c = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 7);
        await _client.CommitWithRevealAsync(drawId, participants[0].ParticipantId, participants[0].Secret, c.Commitment, c.Number, c.SecretHex);

        var drawBody = await (await _client.GetDrawAsync(drawId)).Content.ReadAsStringAsync();
        var verification = await (await _client.GetVerificationAsync(drawId)).ReadAsAsync<VerificationResponse>();
        var verificationBody = await (await _client.GetVerificationAsync(drawId)).Content.ReadAsStringAsync();

        Assert.Equal("Committing", verification.State);
        Assert.Empty(verification.Reveals);
        Assert.DoesNotContain(c.SecretHex, drawBody);
        Assert.DoesNotContain(c.SecretHex, verificationBody);
        Assert.DoesNotContain("\"number\":7", drawBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommitWithReveal_Mismatch_Returns422AndStoresNothing()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var c = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 7);

        var response = await _client.CommitWithRevealAsync(drawId, participants[0].ParticipantId, participants[0].Secret, c.Commitment, 8, c.SecretHex);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var draw = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Open", draw.State);
        Assert.Equal(0, draw.CommitCount);

        // The participant can still commit correctly afterwards.
        var retry = await _client.CommitWithRevealAsync(drawId, participants[0].ParticipantId, participants[0].Secret, c.Commitment, c.Number, c.SecretHex);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Theory]
    [InlineData(0, "ok")]
    [InlineData(11, "ok")]
    [InlineData(5, "not-hex")]
    public async Task CommitWithReveal_InvalidRevealFormat_Returns400(int number, string secretKind)
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var c = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 5);
        var secret = secretKind == "ok" ? c.SecretHex : "zz";

        var response = await _client.CommitWithRevealAsync(drawId, participants[0].ParticipantId, participants[0].Secret, c.Commitment, number, secret);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MixedClients_FallBackToRevealing_UntilMissingRevealArrives()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var withReveal = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 4);
        var hashOnly = DrawFlow.BuildCommitment(drawId, participants[1].ParticipantId, 6);

        await _client.CommitWithRevealAsync(drawId, participants[0].ParticipantId, participants[0].Secret, withReveal.Commitment, withReveal.Number, withReveal.SecretHex);
        await _client.CommitAsync(drawId, participants[1].ParticipantId, participants[1].Secret, hashOnly.Commitment);

        var revealing = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Revealing", revealing.State);
        Assert.NotNull(revealing.Participants.Single(p => p.Id == participants[0].ParticipantId).Reveal);
        Assert.Null(revealing.Participants.Single(p => p.Id == participants[1].ParticipantId).Reveal);

        var reveal = await _client.RevealAsync(drawId, participants[1].ParticipantId, participants[1].Secret, hashOnly.Number, hashOnly.SecretHex);
        Assert.Equal(HttpStatusCode.OK, reveal.StatusCode);

        var completed = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Completed", completed.State);
        Assert.NotNull(completed.Result);
    }
}
