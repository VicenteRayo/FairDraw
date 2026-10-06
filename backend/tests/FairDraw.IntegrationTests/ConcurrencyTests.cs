using System.Net;
using FairDraw.Api.Dtos;

namespace FairDraw.IntegrationTests;

/// <summary>
/// Exercises the "last commit/reveal" race described in CommitmentService/RevealService. Against
/// SQLite (used here) this mostly proves the outcome is at least CORRECT (exactly one transition,
/// no corrupted/stuck state) even if SQLite's own single-writer locking means it doesn't reproduce
/// the exact Postgres MVCC race the Serializable+retry logic targets — see plan §5's documented
/// caveat that only a real Postgres run can fully exercise that specific contention path.
/// </summary>
public class ConcurrencyTests(FairDrawApiFactory factory) : IClassFixture<FairDrawApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SimultaneousLastCommits_TransitionExactlyOnceToRevealing()
    {
        var names = new List<string> { "Ana", "Bruno", "Carla", "Diego" };
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], names);

        // First two commit sequentially to set up the "last two" race.
        for (var i = 0; i < 2; i++)
        {
            var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, participants[i].ParticipantId, 5);
            await _client.CommitAsync(drawId, participants[i].ParticipantId, participants[i].Secret, commitment);
        }

        var lastTwo = participants.Skip(2).ToList();
        var tasks = lastTwo.Select(p =>
        {
            var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5);
            return _client.CommitAsync(drawId, p.ParticipantId, p.Secret, commitment);
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var draw = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Revealing", draw.State);
        Assert.Equal(4, draw.CommitCount);
    }

    [Fact]
    public async Task SimultaneousLastReveals_CompletesExactlyOnceWithOneResult()
    {
        var names = new List<string> { "Ana", "Bruno", "Carla", "Diego" };
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], names);

        var plans = participants.Select((p, i) => (p, DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5 + i))).ToList();
        foreach (var (p, c) in plans)
        {
            await _client.CommitAsync(drawId, p.ParticipantId, p.Secret, c.Commitment);
        }

        for (var i = 0; i < 2; i++)
        {
            var (p, c) = plans[i];
            await _client.RevealAsync(drawId, p.ParticipantId, p.Secret, c.Number, c.SecretHex);
        }

        var lastTwoTasks = plans.Skip(2).Select(entry =>
        {
            var (p, c) = entry;
            return _client.RevealAsync(drawId, p.ParticipantId, p.Secret, c.Number, c.SecretHex);
        });

        var responses = await Task.WhenAll(lastTwoTasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var draw = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Completed", draw.State);
        Assert.NotNull(draw.Result);
    }

    [Fact]
    public async Task SimultaneousLastCommitsWithReveal_CompletesExactlyOnceWithOneResult()
    {
        var names = new List<string> { "Ana", "Bruno", "Carla", "Diego" };
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], names);
        var plans = participants.Select((p, i) => (p, DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5 + i))).ToList();

        for (var i = 0; i < 2; i++)
        {
            var (p, c) = plans[i];
            await _client.CommitWithRevealAsync(drawId, p.ParticipantId, p.Secret, c.Commitment, c.Number, c.SecretHex);
        }

        var responses = await Task.WhenAll(plans.Skip(2).Select(entry =>
        {
            var (p, c) = entry;
            return _client.CommitWithRevealAsync(drawId, p.ParticipantId, p.Secret, c.Commitment, c.Number, c.SecretHex);
        }));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var draw = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Completed", draw.State);
        Assert.NotNull(draw.Result);
    }
}
