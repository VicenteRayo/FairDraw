using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FairDraw.Crypto;

/// <summary>
/// Deterministic, reproducible, unbiased selection of a winning index from a fixed result hash.
///
/// A naive `uint64(firstBytes) % N` introduces modulo bias whenever N does not evenly divide 2^64:
/// values in the "leftover" region above the largest multiple of N are slightly more likely to map
/// to the low indices. Since we only have ONE fixed hash (not a resamplable random stream — re-running
/// the reveal phase to get a new hash would itself be a manipulation vector), we instead use a
/// deterministic counter-mode rejection loop: re-hash (resultHash || attempt) until the derived value
/// falls inside the unbiased region. This is fully deterministic and reproducible by anyone re-running
/// the same public data, and must produce byte-identical results in the TypeScript implementation
/// (frontend/src/crypto/rejectionSampling.ts) for the same inputs — see test-vectors/canonical-vectors.json.
/// </summary>
public static class RejectionSampling
{
    /// <summary>
    /// seed = "{resultHashHex}|{attempt}" (attempt: decimal, starting at 0, no leading zeros)
    /// candidateHash = SHA256(UTF8(seed))
    /// value = big-endian uint64 of the first 8 bytes of candidateHash
    /// limit = floor(2^64 / N) * N
    /// while value >= limit: attempt++; recompute
    /// winnerIndex = value % N
    ///
    /// <paramref name="hashFunction"/> is injectable so tests can force the rejection branch
    /// deterministically; with real SHA-256 the probability of ever needing more than one attempt
    /// is astronomically small for any realistic option count.
    /// </summary>
    public static (int WinnerIndex, int AttemptsUsed) SelectWinnerIndex(
        string resultHashHex,
        int optionCount,
        Func<string, byte[]>? hashFunction = null)
    {
        if (optionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(optionCount), "optionCount must be positive.");
        }

        hashFunction ??= DefaultHash;

        // 2^64 does not fit in a ulong (max is 2^64 - 1), so the "total space" and "limit"
        // computation is done in UInt128 to allow the exact case where N divides 2^64 evenly
        // (limit == 2^64 == 1 << 64), which must accept every possible ulong value.
        UInt128 totalSpace = (UInt128)1 << 64;
        UInt128 n = (UInt128)(uint)optionCount;
        UInt128 limit = totalSpace / n * n;

        var attempt = 0;
        while (true)
        {
            var seed = $"{resultHashHex}|{attempt}";
            var candidate = hashFunction(seed);
            var value = BinaryPrimitives.ReadUInt64BigEndian(candidate.AsSpan(0, 8));

            if ((UInt128)value < limit)
            {
                var winnerIndex = (int)(value % (ulong)optionCount);
                return (winnerIndex, attempt);
            }

            attempt++;
        }
    }

    private static byte[] DefaultHash(string input) => SHA256.HashData(Encoding.UTF8.GetBytes(input));
}
