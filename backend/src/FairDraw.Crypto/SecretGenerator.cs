using System.Security.Cryptography;

namespace FairDraw.Crypto;

/// <summary>
/// Cryptographically secure random generation for secrets and claim tokens, using
/// System.Security.Cryptography.RandomNumberGenerator only — no custom PRNG.
/// </summary>
public static class SecretGenerator
{
    /// <summary>Generates <paramref name="byteLength"/> random bytes, returned as lowercase hex.</summary>
    public static string GenerateHex(int byteLength)
    {
        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        }

        return Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(byteLength));
    }

    /// <summary>
    /// Uniformly random integer in [min, max] (inclusive), via rejection sampling on a single byte
    /// to avoid modulo bias. Used to generate a participant's own number (1-10); this is a hygiene
    /// choice, not a security-critical one — the property that must be bias-free is the mapping from
    /// the final result hash to the winning option (see <see cref="RejectionSampling"/>), not how an
    /// individual participant happens to pick their own number.
    /// </summary>
    public static int GenerateNumberInRange(int min, int max)
    {
        var range = max - min + 1;
        if (range is <= 0 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Range must be within (0, 256].");
        }

        var limit = 256 / range * range;
        while (true)
        {
            var b = RandomNumberGenerator.GetBytes(1)[0];
            if (b < limit)
            {
                return min + b % range;
            }
        }
    }
}
