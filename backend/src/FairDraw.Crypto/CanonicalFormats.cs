using System.Security.Cryptography;
using System.Text;

namespace FairDraw.Crypto;

/// <summary>
/// Exact, unambiguous canonical string formats used throughout the Commit-Reveal protocol.
/// These MUST match, byte for byte, the TypeScript implementation in frontend/src/crypto/canonical.ts —
/// see test-vectors/canonical-vectors.json for the shared golden vectors both sides are tested against.
///
/// Global rules:
///  - every canonical string is UTF-8 encoded before hashing with SHA-256;
///  - every hash is represented as lowercase hex, no "0x" prefix;
///  - "drawId" always means the short public code (Draw.PublicCode), never the internal Guid —
///    this keeps the verification page independent from database implementation details;
///  - option and participant names must never contain '|' (validated at creation time via
///    <see cref="IsSafeForCanonical"/>) so the pipe-joined string can always be reconstructed unambiguously.
/// </summary>
public static class CanonicalFormats
{
    public const string ProtocolVersion = "1";

    /// <summary>SHA-256 of the UTF-8 bytes of <paramref name="input"/>, as lowercase hex.</summary>
    public static string Sha256Hex(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Returns true if the value contains no character that would break canonical parsing.</summary>
    public static bool IsSafeForCanonical(string value) => !value.Contains('|');

    /// <summary>
    /// "{drawId}|{OPTION_1}|...|{OPTION_M}|{PARTICIPANT_1}|...|{PARTICIPANT_N}"
    /// Options and participants must already be in ascending SortOrder as fixed at Lock time.
    /// </summary>
    public static string BuildConfigurationCanonical(
        string drawId,
        IReadOnlyList<string> optionsInOrder,
        IReadOnlyList<string> participantsInOrder)
    {
        var parts = new List<string>(1 + optionsInOrder.Count + participantsInOrder.Count) { drawId };
        parts.AddRange(optionsInOrder);
        parts.AddRange(participantsInOrder);
        return string.Join('|', parts);
    }

    /// <summary>
    /// "version=1|drawId={drawId}|participantId={participantId}|number={number}|secret={secretHex}"
    /// participantId is the participant's Guid exactly as issued by the API (36 chars, lowercase, hyphenated).
    /// number is a decimal integer 1-10 with no leading zeros. secretHex is 64 lowercase hex chars (32 bytes).
    /// </summary>
    public static string BuildCommitmentCanonical(string drawId, string participantId, int number, string secretHex)
        => $"version={ProtocolVersion}|drawId={drawId}|participantId={participantId}|number={number}|secret={secretHex}";

    /// <summary>
    /// "version=1|drawId={drawId}|reveals={pid1}:{num1},{pid2}:{num2},...,{pidN}:{numN}"
    /// Reveals are sorted by participantId as a plain ordinal string comparison before joining —
    /// this must match JavaScript's default Array.sort() on the same (ASCII) Guid strings.
    /// </summary>
    public static string BuildRevealedDataCanonical(string drawId, IReadOnlyList<(string ParticipantId, int Number)> reveals)
    {
        var ordered = reveals.OrderBy(r => r.ParticipantId, StringComparer.Ordinal);
        var joined = string.Join(',', ordered.Select(r => $"{r.ParticipantId}:{r.Number}"));
        return $"version={ProtocolVersion}|drawId={drawId}|reveals={joined}";
    }
}
