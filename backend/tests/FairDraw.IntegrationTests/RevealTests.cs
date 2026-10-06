using System.Net;
using FairDraw.Api.Dtos;
using FairDraw.Crypto;

namespace FairDraw.IntegrationTests;

public class RevealTests(FairDrawApiFactory factory) : IClassFixture<FairDrawApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(string DrawId, List<(DrawFlow.ParticipantContext Ctx, int Number, string SecretHex)> Data)> SetupRevealingDrawAsync(
        List<string> options, List<string> names, List<int> numbers)
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", options, names);
        var data = new List<(DrawFlow.ParticipantContext, int, string)>();

        for (var i = 0; i < participants.Count; i++)
        {
            var (number, secretHex, commitment) = DrawFlow.BuildCommitment(drawId, participants[i].ParticipantId, numbers[i]);
            var response = await _client.CommitAsync(drawId, participants[i].ParticipantId, participants[i].Secret, commitment);
            response.EnsureSuccessStatusCode();
            data.Add((participants[i], number, secretHex));
        }

        return (drawId, data);
    }

    [Fact]
    public async Task Reveal_BeforeAllCommitted_Returns409()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (number, secretHex, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 5);
        await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, commitment);

        var response = await _client.RevealAsync(drawId, participants[0].ParticipantId, participants[0].Secret, number, secretHex);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reveal_Valid_ReturnsOkAndMarksValid()
    {
        var (drawId, data) = await SetupRevealingDrawAsync(["A", "B"], ["Ana", "Bruno"], [5, 7]);

        var response = await _client.RevealAsync(drawId, data[0].Ctx.ParticipantId, data[0].Ctx.Secret, data[0].Number, data[0].SecretHex);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsAsync<RevealResponse>();
        Assert.True(body.Valid);
    }

    [Fact]
    public async Task Reveal_WrongNumber_Returns422AndDoesNotComplete()
    {
        var (drawId, data) = await SetupRevealingDrawAsync(["A", "B"], ["Ana", "Bruno"], [5, 7]);

        var response = await _client.RevealAsync(drawId, data[0].Ctx.ParticipantId, data[0].Ctx.Secret, data[0].Number + 1, data[0].SecretHex);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var draw = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Revealing", draw.State);
    }

    [Fact]
    public async Task Reveal_WrongSecret_Returns422()
    {
        var (drawId, data) = await SetupRevealingDrawAsync(["A", "B"], ["Ana", "Bruno"], [5, 7]);
        var wrongSecret = SecretGenerator.GenerateHex(32);

        var response = await _client.RevealAsync(drawId, data[0].Ctx.ParticipantId, data[0].Ctx.Secret, data[0].Number, wrongSecret);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Reveal_WithWrongParticipantCredential_Returns401()
    {
        var (drawId, data) = await SetupRevealingDrawAsync(["A", "B"], ["Ana", "Bruno"], [5, 7]);

        var response = await _client.RevealAsync(drawId, data[0].Ctx.ParticipantId, data[1].Ctx.Secret, data[0].Number, data[0].SecretHex);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reveal_RetryAfterInvalid_CanSucceed()
    {
        var (drawId, data) = await SetupRevealingDrawAsync(["A", "B"], ["Ana", "Bruno"], [5, 7]);

        var badAttempt = await _client.RevealAsync(drawId, data[0].Ctx.ParticipantId, data[0].Ctx.Secret, data[0].Number + 1, data[0].SecretHex);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badAttempt.StatusCode);

        var goodAttempt = await _client.RevealAsync(drawId, data[0].Ctx.ParticipantId, data[0].Ctx.Secret, data[0].Number, data[0].SecretHex);
        Assert.Equal(HttpStatusCode.OK, goodAttempt.StatusCode);
    }

    [Fact]
    public async Task Reveal_AfterAlreadyValid_Returns409()
    {
        var (drawId, data) = await SetupRevealingDrawAsync(["A", "B"], ["Ana", "Bruno"], [5, 7]);
        await _client.RevealAsync(drawId, data[0].Ctx.ParticipantId, data[0].Ctx.Secret, data[0].Number, data[0].SecretHex);

        var response = await _client.RevealAsync(drawId, data[0].Ctx.ParticipantId, data[0].Ctx.Secret, data[0].Number, data[0].SecretHex);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reveal_AllValid_CompletesDrawAndComputesResult()
    {
        var (drawId, data) = await SetupRevealingDrawAsync(["A", "B"], ["Ana", "Bruno"], [5, 7]);

        foreach (var (ctx, number, secretHex) in data)
        {
            var response = await _client.RevealAsync(drawId, ctx.ParticipantId, ctx.Secret, number, secretHex);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var draw = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Completed", draw.State);
        Assert.NotNull(draw.Result);
        Assert.InRange(draw.Result!.WinnerIndex, 0, 1);
    }

    [Fact]
    public async Task Reveal_InvalidAttempt_NeverExposesSubmittedNumberOrSecret()
    {
        var (drawId, data) = await SetupRevealingDrawAsync(["A", "B"], ["Ana", "Bruno"], [5, 7]);

        await _client.RevealAsync(drawId, data[0].Ctx.ParticipantId, data[0].Ctx.Secret, 10, data[0].SecretHex);

        var body = await (await _client.GetDrawAsync(drawId)).Content.ReadAsStringAsync();
        Assert.DoesNotContain(data[0].SecretHex, body);
    }
}
