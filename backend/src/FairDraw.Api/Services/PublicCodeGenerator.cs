using System.Security.Cryptography;

namespace FairDraw.Api.Services;

/// <summary>
/// Generates short public routing codes such as "8F4K92LM". Uses a Crockford-like alphabet that
/// drops visually ambiguous characters (0/O, 1/I/L) so codes are easy to read aloud or retype.
/// </summary>
public static class PublicCodeGenerator
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int Length = 8;

    public static string Generate()
    {
        Span<char> chars = stackalloc char[Length];
        var bytes = RandomNumberGenerator.GetBytes(Length);
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
