using System.Net;
using FairDraw.Api.Dtos;

namespace FairDraw.IntegrationTests;

public class VerificationVisibilityTests(FairDrawApiFactory factory) : IClassFixture<FairDrawApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Verification_UnknownDraw_Returns404()
    {
        var response = await _client.GetVerificationAsync("NOTFOUND");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Verification_BeforeReveal_NeverExposesNumbersOrSecrets()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (_, secretHex, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 7);
        await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, commitment);

        var verification = await (await _client.GetVerificationAsync(drawId)).ReadAsAsync<VerificationResponse>();

        Assert.Equal("Committing", verification.State);
        Assert.Single(verification.Commitments);
        Assert.Empty(verification.Reveals);
        Assert.Null(verification.Result);
        var raw = await (await _client.GetVerificationAsync(drawId)).Content.ReadAsStringAsync();
        Assert.DoesNotContain(secretHex, raw);
    }

    [Fact]
    public async Task Verification_DuringRevealing_OnlyExposesValidReveals()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var commits = participants.Select(p => (p, DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5))).ToList();
        foreach (var (p, c) in commits)
        {
            await _client.CommitAsync(drawId, p.ParticipantId, p.Secret, c.Commitment);
        }

        // Only the first participant reveals (correctly); the second hasn't yet.
        var (number0, secretHex0, _) = commits[0].Item2;
        await _client.RevealAsync(drawId, commits[0].p.ParticipantId, commits[0].p.Secret, number0, secretHex0);

        var verification = await (await _client.GetVerificationAsync(drawId)).ReadAsAsync<VerificationResponse>();

        Assert.Equal("Revealing", verification.State);
        Assert.Single(verification.Reveals);
        Assert.Equal(2, verification.Commitments.Count);
        Assert.Null(verification.Result);
    }

    [Fact]
    public async Task Verification_AfterCompletion_IncludesFullResultData()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var commits = participants.Select(p => (p, DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5))).ToList();
        foreach (var (p, c) in commits)
        {
            await _client.CommitAsync(drawId, p.ParticipantId, p.Secret, c.Commitment);
        }
        foreach (var (p, c) in commits)
        {
            await _client.RevealAsync(drawId, p.ParticipantId, p.Secret, c.Number, c.SecretHex);
        }

        var verification = await (await _client.GetVerificationAsync(drawId)).ReadAsAsync<VerificationResponse>();

        Assert.Equal("Completed", verification.State);
        Assert.Equal(2, verification.Reveals.Count);
        Assert.NotNull(verification.Result);
        Assert.NotNull(verification.ConfigurationHash);
    }
}
