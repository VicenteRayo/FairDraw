using System.Text.Json;
using System.Text.Json.Serialization;

namespace FairDraw.UnitTests;

/// <summary>
/// Loads test-vectors/canonical-vectors.json, the single source of truth shared with the
/// frontend's Vitest suite, so both implementations are checked against the exact same data.
/// </summary>
public static class GoldenVectors
{
    private static readonly Lazy<VectorsDocument> Instance = new(Load);

    public static VectorsDocument Data => Instance.Value;

    private static VectorsDocument Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "canonical-vectors.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VectorsDocument>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) ?? throw new InvalidOperationException("canonical-vectors.json deserialized to null.");
    }

    public record VectorsDocument(
        [property: JsonPropertyName("protocolVersion")] int ProtocolVersion,
        [property: JsonPropertyName("drawId")] string DrawId,
        [property: JsonPropertyName("options")] List<string> Options,
        [property: JsonPropertyName("participantsNames")] List<string> ParticipantsNames,
        [property: JsonPropertyName("configurationCanonical")] string ConfigurationCanonical,
        [property: JsonPropertyName("configurationHash")] string ConfigurationHash,
        [property: JsonPropertyName("participants")] List<ParticipantVector> Participants,
        [property: JsonPropertyName("resultCanonical")] string ResultCanonical,
        [property: JsonPropertyName("resultHash")] string ResultHash,
        [property: JsonPropertyName("optionCount")] int OptionCount,
        [property: JsonPropertyName("winnerIndex")] int WinnerIndex,
        [property: JsonPropertyName("attemptsUsed")] int AttemptsUsed,
        [property: JsonPropertyName("winnerOption")] string WinnerOption
    );

    public record ParticipantVector(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("participantId")] string ParticipantId,
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("secretHex")] string SecretHex,
        [property: JsonPropertyName("commitmentCanonical")] string CommitmentCanonical,
        [property: JsonPropertyName("commitmentHash")] string CommitmentHash
    );
}
