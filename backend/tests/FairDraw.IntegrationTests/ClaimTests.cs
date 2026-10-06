using System.Net;
using FairDraw.Api.Dtos;

namespace FairDraw.IntegrationTests;

public class ClaimTests(FairDrawApiFactory factory) : IClassFixture<FairDrawApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Claim_BeforeLock_Returns409()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["Ana", "Bruno"])).ReadAsAsync<CreateDrawResponse>();

        var response = await _client.ClaimAsync(create.DrawId, create.Draw.Participants[0].Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Claim_FirstTime_Succeeds()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["Ana", "Bruno"])).ReadAsAsync<CreateDrawResponse>();
        await _client.LockDrawAsync(create.DrawId, create.CreatorToken);

        var response = await _client.ClaimAsync(create.DrawId, create.Draw.Participants[0].Id);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var claim = await response.ReadAsAsync<ClaimResponse>();
        Assert.Equal(64, claim.ParticipantSecret.Length);
    }

    [Fact]
    public async Task Claim_Twice_SecondReturns403()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["Ana", "Bruno"])).ReadAsAsync<CreateDrawResponse>();
        await _client.LockDrawAsync(create.DrawId, create.CreatorToken);
        var participantId = create.Draw.Participants[0].Id;

        var first = await _client.ClaimAsync(create.DrawId, participantId);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.ClaimAsync(create.DrawId, participantId);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
    }

    [Fact]
    public async Task Claim_UnknownParticipant_Returns404()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["Ana", "Bruno"])).ReadAsAsync<CreateDrawResponse>();
        await _client.LockDrawAsync(create.DrawId, create.CreatorToken);

        var response = await _client.ClaimAsync(create.DrawId, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Claim_ConcurrentAttemptsOnSameName_OnlyOneSucceeds()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["Ana", "Bruno"])).ReadAsAsync<CreateDrawResponse>();
        await _client.LockDrawAsync(create.DrawId, create.CreatorToken);
        var participantId = create.Draw.Participants[0].Id;

        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _client.ClaimAsync(create.DrawId, participantId)));

        var successCount = attempts.Count(r => r.StatusCode == HttpStatusCode.OK);
        var forbiddenCount = attempts.Count(r => r.StatusCode == HttpStatusCode.Forbidden);

        Assert.Equal(1, successCount);
        Assert.Equal(7, forbiddenCount);
    }
}
