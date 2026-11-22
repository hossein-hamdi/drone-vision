using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Checker.Domain.ValueObjects;

/// <summary>
/// Validated IEEE 802.11 SSID value object (1..32 UTF-8 bytes, no ASCII control characters).
/// </summary>
public sealed class SsidName : IEquatable<SsidName>
{
    public const int MaxUtf8Bytes = 32;

    private SsidName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(
        string? raw,
        [MaybeNullWhen(false)] out SsidName result,
        out string errorMessage)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            errorMessage = "SSID cannot be empty or whitespace when provided.";
            return false;
        }

        var trimmed = raw.Trim();
        if (Encoding.UTF8.GetByteCount(trimmed) > MaxUtf8Bytes)
        {
            errorMessage = $"SSID must not exceed {MaxUtf8Bytes} UTF-8 bytes.";
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (ch < 0x20 || ch == 0x7F)
            {
                errorMessage = "SSID must not contain control characters.";
                return false;
            }
        }

        result = new SsidName(trimmed);
        errorMessage = string.Empty;
        return true;
    }

    public bool Equals(SsidName? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as SsidName);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;
}
