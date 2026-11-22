using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Checker.Domain.ValueObjects;

/// <summary>
/// Validated and normalized 48-bit IEEE 802 MAC / BSSID value object.
/// </summary>
public sealed class MacAddress : IParsable<MacAddress>, IEquatable<MacAddress>
{
    private MacAddress(byte[] bytes)
    {
        RawBytes = bytes;
        CompactHex = Convert.ToHexString(bytes);
        Canonical = string.Create(17, bytes, static (span, b) =>
        {
            const string hex = "0123456789ABCDEF";
            for (var i = 0; i < 6; i++)
            {
                var pos = i * 3;
                span[pos] = hex[b[i] >> 4];
                span[pos + 1] = hex[b[i] & 0x0F];
                if (i < 5)
                {
                    span[pos + 2] = ':';
                }
            }
        });
        Oui24 = Canonical[..8];
        Prefix28 = CompactHex[..7];
        Prefix36 = CompactHex[..9];
        IsMulticast = (bytes[0] & 0x01) != 0;
        IsLocallyAdministered = (bytes[0] & 0x02) != 0;
    }

    public byte[] RawBytes { get; }
    public string Canonical { get; }
    public string CompactHex { get; }
    public string Oui24 { get; }
    public string Prefix28 { get; }
    public string Prefix36 { get; }
    public bool IsLocallyAdministered { get; }
    public bool IsMulticast { get; }

    public static string FormatPrefix(string compactPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compactPrefix);
        var upper = compactPrefix.ToUpperInvariant();
        return upper.Length switch
        {
            6 => $"{upper[..2]}:{upper[2..4]}:{upper[4..6]}",
            7 => $"{upper[..2]}:{upper[2..4]}:{upper[4..6]}:{upper[6]}",
            9 => $"{upper[..2]}:{upper[2..4]}:{upper[4..6]}:{upper[6..8]}:{upper[8]}",
            _ => upper
        };
    }

    public static MacAddress Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var result)
            ? result
            : throw new FormatException("Invalid MAC/BSSID address format.");

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        [MaybeNullWhen(false)] out MacAddress result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        var trimmed = s.Trim();
        Span<char> hex = stackalloc char[12];
        if (!TryExtractNormalizedHex(trimmed, hex))
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[6];
        for (var i = 0; i < 6; i++)
        {
            if (!byte.TryParse(hex.Slice(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
            {
                return false;
            }
        }

        var isAllZero = true;
        var isBroadcast = true;
        for (var i = 0; i < 6; i++)
        {
            if (bytes[i] != 0x00)
            {
                isAllZero = false;
            }

            if (bytes[i] != 0xFF)
            {
                isBroadcast = false;
            }
        }

        if (isAllZero || isBroadcast)
        {
            return false;
        }

        result = new MacAddress(bytes.ToArray());
        return true;
    }

    private static bool TryExtractNormalizedHex(string input, Span<char> hexDest)
    {
        // Bare 12 hex digits
        if (input.Length == 12)
        {
            return CopyHex(input, hexDest);
        }

        // XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX
        if (input.Length == 17)
        {
            var sep = input[2];
            if (sep is not (':' or '-'))
            {
                return false;
            }

            var outIdx = 0;
            for (var i = 0; i < 17; i++)
            {
                if ((i + 1) % 3 == 0)
                {
                    if (input[i] != sep)
                    {
                        return false;
                    }

                    continue;
                }

                if (!Uri.IsHexDigit(input[i]))
                {
                    return false;
                }

                hexDest[outIdx++] = char.ToUpperInvariant(input[i]);
            }

            return outIdx == 12;
        }

        // XXXX.XXXX.XXXX (Cisco dot notation)
        if (input.Length == 14 && input[4] == '.' && input[9] == '.')
        {
            var outIdx = 0;
            for (var i = 0; i < 14; i++)
            {
                if (i is 4 or 9)
                {
                    continue;
                }

                if (!Uri.IsHexDigit(input[i]))
                {
                    return false;
                }

                hexDest[outIdx++] = char.ToUpperInvariant(input[i]);
            }

            return outIdx == 12;
        }

        return false;
    }

    private static bool CopyHex(string input, Span<char> hexDest)
    {
        for (var i = 0; i < 12; i++)
        {
            if (!Uri.IsHexDigit(input[i]))
            {
                return false;
            }

            hexDest[i] = char.ToUpperInvariant(input[i]);
        }

        return true;
    }

    public bool Equals(MacAddress? other) => other is not null && Canonical == other.Canonical;
    public override bool Equals(object? obj) => Equals(obj as MacAddress);
    public override int GetHashCode() => Canonical.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Canonical;
}
