using System.Net;
using FairDraw.Api.Dtos;
using FairDraw.Crypto;
using FairDraw.UnitTests;

namespace FairDraw.IntegrationTests;

/// <summary>
/// Drives the full API (create -> lock -> claim -> commit -> reveal) using the exact same
/// participants/numbers/secrets as test-vectors/canonical-vectors.json.
///
/// Note: participantId in the golden vectors is a *fixed, arbitrary* GUID chosen for the
/// documentation example; the live API always generates its own participant GUIDs, and the
/// winnerIndex formula depends on those exact GUID strings (see CanonicalFormats). So this test
/// does NOT assert the server reproduces the vector file's fixed winnerIndex — that would only
/// hold if the participantIds matched too. Instead it independently recomputes the expected
/// resultHash/winnerIndex from the REAL server-issued participantIds using the same FairDraw.Crypto
/// functions already validated against the golden vectors in FairDraw.UnitTests, and asserts the
/// live API's result matches that independent computation — the same check the public /verify page
/// performs against a real deployment.
/// </summary>
public class EndToEndGoldenVectorTests(FairDrawApiFactory factory) : IClassFixture<FairDrawApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FullFlow_WithGoldenVectorNumbersAndSecrets_ProducesExpectedWinner()
    {
        var vectors = GoldenVectors.Data;

        var create = await (await _client.CreateDrawAsync(
            "Golden vector draw",
            vectors.Options,
            vectors.Participants.Select(p => p.Name).ToList())).ReadAsAsync<CreateDrawResponse>();

        await (await _client.LockDrawAsync(create.DrawId, create.CreatorToken)).EnsureSuccess();

        // Server-issued participantIds differ from the vector file's fixed example IDs, so
        // commitments must be recomputed against the real IDs — using the exact same secrets and
        // numbers as the vectors, and the exact same canonical formats (already unit-tested).
        var byName = create.Draw.Participants.ToDictionary(p => p.Name);
        var plan = vectors.Participants.Select(v => new
        {
            v.Name,
            v.Number,
            v.SecretHex,
            ParticipantId = byName[v.Name].Id
        }).ToList();

        var secrets = new Dictionary<string, string>();
        foreach (var p in plan)
        {
            var claim = await (await _client.ClaimAsync(create.DrawId, p.ParticipantId)).ReadAsAsync<ClaimResponse>();
            secrets[p.ParticipantId] = claim.ParticipantSecret;
        }

        foreach (var p in plan)
        {
            var (_, _, commitment) = DrawFlow.BuildCommitment(create.DrawId, p.ParticipantId, p.Number, p.SecretHex);
            var response = await _client.CommitAsync(create.DrawId, p.ParticipantId, secrets[p.ParticipantId], commitment);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        foreach (var p in plan)
        {
            var response = await _client.RevealAsync(create.DrawId, p.ParticipantId, secrets[p.ParticipantId], p.Number, p.SecretHex);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var draw = await (await _client.GetDrawAsync(create.DrawId)).ReadAsAsync<DrawDto>();

        Assert.Equal("Completed", draw.State);
        Assert.NotNull(draw.Result);

        // Independently recompute the expected outcome from the REAL server-issued participantIds,
        // using the exact same functions FairDraw.UnitTests already validated against the golden
        // vectors — this is what makes the assertion below meaningful rather than circular.
        var revealTuples = plan.Select(p => (p.ParticipantId.ToString(), p.Number)).ToList();
        var canonicalRevealed = CanonicalFormats.BuildRevealedDataCanonical(create.DrawId, revealTuples);
        var expectedResultHash = CanonicalFormats.Sha256Hex(canonicalRevealed);
        var (expectedWinnerIndex, expectedAttemptsUsed) = RejectionSampling.SelectWinnerIndex(expectedResultHash, vectors.Options.Count);
        var expectedWinnerOption = vectors.Options[expectedWinnerIndex];

        Assert.Equal(expectedResultHash, draw.Result!.ResultHash);
        Assert.Equal(expectedWinnerIndex, draw.Result.WinnerIndex);
        Assert.Equal(expectedAttemptsUsed, draw.Result.AttemptsUsed);
        Assert.Equal(expectedWinnerOption, draw.Result.WinnerOptionText);

        // And the independently-computable /verification payload must agree too.
        var verification = await (await _client.GetVerificationAsync(create.DrawId)).ReadAsAsync<VerificationResponse>();
        Assert.Equal(expectedWinnerIndex, verification.Result!.WinnerIndex);
        Assert.Equal(expectedResultHash, verification.Result.ResultHash);
    }
}

file static class HttpResponseExtensions
{
    public static Task<HttpResponseMessage> EnsureSuccess(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return Task.FromResult(response);
    }
}
