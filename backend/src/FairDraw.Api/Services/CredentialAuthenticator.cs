using System.Security.Cryptography;
using System.Text;
using FairDraw.Api.Errors;
using FairDraw.Crypto;

namespace FairDraw.Api.Services;

/// <summary>
/// Verifies bearer credentials (creator token or participant claim secret) against their stored
/// SHA-256 hash using a fixed-time comparison, to avoid leaking hash-matching progress via timing.
/// </summary>
public static class CredentialAuthenticator
{
    public static void Verify(string? presentedToken, string? storedHash, string missingCode, string invalidCode)
    {
        if (string.IsNullOrEmpty(presentedToken))
        {
            throw ApiErrors.Unauthorized(missingCode);
        }

        if (storedHash is null)
        {
            // Nothing has been issued yet for this identity (e.g. participant hasn't claimed).
            throw ApiErrors.Unauthorized(missingCode);
        }

        var presentedHash = CanonicalFormats.Sha256Hex(presentedToken);
        var equal = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(presentedHash),
            Encoding.ASCII.GetBytes(storedHash));

        if (!equal)
        {
            throw ApiErrors.Unauthorized(invalidCode);
        }
    }

    public static string? ExtractBearerToken(string? authorizationHeader)
    {
        const string prefix = "Bearer ";
        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return authorizationHeader[prefix.Length..].Trim();
    }
}
