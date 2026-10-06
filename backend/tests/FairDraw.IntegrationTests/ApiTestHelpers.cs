using System.Net.Http.Headers;
using System.Net.Http.Json;
using FairDraw.Api.Dtos;

namespace FairDraw.IntegrationTests;

public static class ApiTestHelpers
{
    private const string BasePath = "/api/v1/draws";

    public static Task<HttpResponseMessage> CreateDrawAsync(this HttpClient client, string name, List<string> options, List<string> participants) =>
        client.PostAsJsonAsync(BasePath, new CreateDrawRequest(name, options, participants));

    public static Task<HttpResponseMessage> GetDrawAsync(this HttpClient client, string code) =>
        client.GetAsync($"{BasePath}/{code}");

    public static Task<HttpResponseMessage> PatchDrawAsync(this HttpClient client, string code, string creatorToken, string name, List<string> options, List<string> participants)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{BasePath}/{code}")
        {
            Content = JsonContent.Create(new UpdateDrawRequest(name, options, participants))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creatorToken);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> LockDrawAsync(this HttpClient client, string code, string creatorToken) =>
        client.PostAuthorizedAsync($"{BasePath}/{code}/lock", creatorToken, null);

    public static Task<HttpResponseMessage> CancelDrawAsync(this HttpClient client, string code, string creatorToken) =>
        client.PostAuthorizedAsync($"{BasePath}/{code}/cancel", creatorToken, null);

    public static Task<HttpResponseMessage> ClaimAsync(this HttpClient client, string code, string participantId) =>
        client.PostAsync($"{BasePath}/{code}/participants/{participantId}/claim", null);

    public static Task<HttpResponseMessage> CommitAsync(this HttpClient client, string code, string participantId, string participantSecret, string commitment) =>
        client.PostAuthorizedAsync($"{BasePath}/{code}/participants/{participantId}/commitments", participantSecret, new CommitRequest(commitment));

    /// <summary>Commit with the reveal attached, as the current frontend does.</summary>
    public static Task<HttpResponseMessage> CommitWithRevealAsync(this HttpClient client, string code, string participantId, string participantSecret, string commitment, int number, string secret) =>
        client.PostAuthorizedAsync($"{BasePath}/{code}/participants/{participantId}/commitments", participantSecret, new CommitRequest(commitment, number, secret));

    public static Task<HttpResponseMessage> RevealAsync(this HttpClient client, string code, string participantId, string participantSecret, int number, string secret) =>
        client.PostAuthorizedAsync($"{BasePath}/{code}/participants/{participantId}/reveals", participantSecret, new RevealRequest(number, secret));

    public static Task<HttpResponseMessage> GetVerificationAsync(this HttpClient client, string code) =>
        client.GetAsync($"{BasePath}/{code}/verification");

    private static Task<HttpResponseMessage> PostAuthorizedAsync(this HttpClient client, string url, string? token, object? body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client.SendAsync(request);
    }

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<T>();
        return result ?? throw new InvalidOperationException($"Response body deserialized to null for {typeof(T).Name}.");
    }
}
