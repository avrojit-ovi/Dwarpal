using System.Security.Cryptography;

namespace Dwarpal.Core.Security;

/// <summary>
/// Recovery key: 160-bit random, Crockford base32 (I/L/O/U nei) - tai confusing character chara
/// print kore kagoje rakha safe. Format: XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX
/// </summary>
public static class RecoveryKey
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford base32
    private const int KeyBytes = 20;      // 160 bit
    private const int EncodedLength = 32; // 160 / 5
    private const int GroupSize = 4;

    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[KeyBytes];
        RandomNumberGenerator.Fill(bytes);

        Span<char> encoded = stackalloc char[EncodedLength];
        uint buffer = 0;
        var bits = 0;
        var index = 0;

        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;

            while (bits >= 5)
            {
                bits -= 5;
                encoded[index++] = Alphabet[(int)((buffer >> bits) & 0x1F)];
            }

            buffer &= (1u << bits) - 1;
        }

        return Format(new string(encoded));
    }

    /// <summary>
    /// User er type kora key ke canonical form e ane (uppercase, dash/space remove, O->0, I/L->1).
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        Span<char> cleaned = stackalloc char[EncodedLength];
        var count = 0;

        foreach (var raw in input.Trim().ToUpperInvariant())
        {
            if (raw is '-' or ' ')
            {
                continue;
            }

            if (count >= EncodedLength)
            {
                return false;
            }

            var character = raw switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                _ => raw,
            };

            if (!Alphabet.Contains(character, StringComparison.Ordinal))
            {
                return false;
            }

            cleaned[count++] = character;
        }

        if (count != EncodedLength)
        {
            return false;
        }

        normalized = Format(new string(cleaned[..count]));
        return true;
    }

    private static string Format(string encoded)
    {
        var groups = new List<string>(EncodedLength / GroupSize);
        for (var offset = 0; offset < encoded.Length; offset += GroupSize)
        {
            groups.Add(encoded.Substring(offset, Math.Min(GroupSize, encoded.Length - offset)));
        }

        return string.Join('-', groups);
    }
}
