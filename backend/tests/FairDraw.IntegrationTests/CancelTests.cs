using System.Net;
using FairDraw.Api.Dtos;

namespace FairDraw.IntegrationTests;

public class CancelTests(FairDrawApiFactory factory) : IClassFixture<FairDrawApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Cancel_FromCreated_Succeeds()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();

        var response = await _client.CancelDrawAsync(create.DrawId, create.CreatorToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draw = await response.ReadAsAsync<DrawDto>();
        Assert.Equal("Cancelled", draw.State);
    }

    [Fact]
    public async Task Cancel_FromOpen_Succeeds()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();
        await _client.LockDrawAsync(create.DrawId, create.CreatorToken);

        var response = await _client.CancelDrawAsync(create.DrawId, create.CreatorToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_FromCommitting_Succeeds()
    {
        var (drawId, creatorToken, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 5);
        await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, commitment);

        var response = await _client.CancelDrawAsync(drawId, creatorToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_FromRevealing_Succeeds()
    {
        var (drawId, creatorToken, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        foreach (var p in participants)
        {
            var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5);
            await _client.CommitAsync(drawId, p.ParticipantId, p.Secret, commitment);
        }

        var response = await _client.CancelDrawAsync(drawId, creatorToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_AfterCompleted_Returns409()
    {
        var (drawId, creatorToken, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var reveals = new List<(string ParticipantId, string Secret, int Number, string SecretHex)>();
        foreach (var p in participants)
        {
            var (number, secretHex, commitment) = DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5);
            await _client.CommitAsync(drawId, p.ParticipantId, p.Secret, commitment);
            reveals.Add((p.ParticipantId, p.Secret, number, secretHex));
        }
        foreach (var r in reveals)
        {
            await _client.RevealAsync(drawId, r.ParticipantId, r.Secret, r.Number, r.SecretHex);
        }

        var response = await _client.CancelDrawAsync(drawId, creatorToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_Twice_SecondReturns409()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();
        await _client.CancelDrawAsync(create.DrawId, create.CreatorToken);

        var response = await _client.CancelDrawAsync(create.DrawId, create.CreatorToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithoutCreatorToken_Returns401()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();

        var response = await _client.CancelDrawAsync(create.DrawId, "");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
