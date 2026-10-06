using System.Net;
using FairDraw.Api.Dtos;

namespace FairDraw.IntegrationTests;

public class CommitTests(FairDrawApiFactory factory) : IClassFixture<FairDrawApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Commit_WithoutAuth_Returns401()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 5);

        var response = await _client.CommitAsync(drawId, participants[0].ParticipantId, "", commitment);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Commit_WithWrongSecret_Returns401()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 5);

        var response = await _client.CommitAsync(drawId, participants[0].ParticipantId, "wrong-secret-not-a-valid-token", commitment);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Commit_UnclaimedParticipant_Returns401()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["Ana", "Bruno"])).ReadAsAsync<CreateDrawResponse>();
        await _client.LockDrawAsync(create.DrawId, create.CreatorToken);
        var participantId = create.Draw.Participants[0].Id;
        var (_, _, commitment) = DrawFlow.BuildCommitment(create.DrawId, participantId, 5);

        var response = await _client.CommitAsync(create.DrawId, participantId, "anything", commitment);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Commit_ValidFirstOfTwo_TransitionsToCommitting()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 5);

        var response = await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, commitment);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var draw = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Committing", draw.State);
        Assert.Equal(1, draw.CommitCount);
    }

    [Fact]
    public async Task Commit_LastOfAll_TransitionsToRevealing()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);

        foreach (var p in participants)
        {
            var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5);
            var response = await _client.CommitAsync(drawId, p.ParticipantId, p.Secret, commitment);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var draw = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Revealing", draw.State);
        Assert.Equal(2, draw.CommitCount);
    }

    [Fact]
    public async Task Commit_Twice_SecondReturns409()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 5);
        await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, commitment);

        var (_, _, commitment2) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 9);
        var response = await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, commitment2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Commit_MalformedHash_Returns400()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);

        var response = await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, "not-a-hash");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Commit_AfterRevealingStarted_Returns409()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        foreach (var p in participants)
        {
            var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5);
            await _client.CommitAsync(drawId, p.ParticipantId, p.Secret, commitment);
        }

        // A third, late-claimed participant should never exist in this model (list frozen at lock),
        // so instead we assert that re-committing (any participant) once in Revealing is rejected.
        var (_, _, lateCommitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 3);
        var response = await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, lateCommitment);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Commit_NeverExposesNumberOrSecretPublicly()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (_, secretHex, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 7);
        await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, commitment);

        var body = await (await _client.GetDrawAsync(drawId)).Content.ReadAsStringAsync();

        Assert.DoesNotContain(secretHex, body);
        Assert.DoesNotContain("\"number\":7", body, StringComparison.OrdinalIgnoreCase);
    }
}
