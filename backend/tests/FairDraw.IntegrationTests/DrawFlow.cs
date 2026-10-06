using FairDraw.Api.Dtos;
using FairDraw.Crypto;

namespace FairDraw.IntegrationTests;

/// <summary>Shared setup helpers so each test file doesn't re-derive the create/lock/claim boilerplate.</summary>
public static class DrawFlow
{
    public record ParticipantContext(string Name, string ParticipantId, string Secret);

    public static async Task<(string DrawId, string CreatorToken, List<ParticipantContext> Participants)> CreateLockAndClaimAsync(
        HttpClient client, string name, List<string> options, List<string> participantNames)
    {
        var create = await (await client.CreateDrawAsync(name, options, participantNames)).ReadAsAsync<CreateDrawResponse>();
        var lockResult = await client.LockDrawAsync(create.DrawId, create.CreatorToken);
        lockResult.EnsureSuccessStatusCode();

        var participants = new List<ParticipantContext>();
        foreach (var p in create.Draw.Participants)
        {
            var claimResponse = await client.ClaimAsync(create.DrawId, p.Id);
            claimResponse.EnsureSuccessStatusCode();
            var claim = await claimResponse.ReadAsAsync<ClaimResponse>();
            participants.Add(new ParticipantContext(p.Name, p.Id, claim.ParticipantSecret));
        }

        return (create.DrawId, create.CreatorToken, participants);
    }

    public static (int Number, string SecretHex, string Commitment) BuildCommitment(
        string drawId, string participantId, int number, string? secretHexOverride = null)
    {
        var secretHex = secretHexOverride ?? SecretGenerator.GenerateHex(32);
        var canonical = CanonicalFormats.BuildCommitmentCanonical(drawId, participantId, number, secretHex);
        var commitment = CanonicalFormats.Sha256Hex(canonical);
        return (number, secretHex, commitment);
    }
}
