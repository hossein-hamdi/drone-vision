using System.Buffers.Binary;
using System.Security.Cryptography;
using Management.Application.Common;
using Management.Application.DTOs;

namespace Management.Infrastructure.Services;

public sealed class ProfileImageStore
{
    public const long MaximumBytes = 5 * 1024 * 1024;
    public const string UrlPrefix = "/uploads/profiles/";
    public async Task<ProfileImageData> ReadAsync(Stream source, long length, CancellationToken ct)
    {
        if (length is <= 0 or > MaximumBytes)
            throw ApiException.Validation("image", "The image must be between 1 byte and 5 MiB.");
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaximumBytes) throw ApiException.Validation("image", "The image exceeds 5 MiB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        var bytes = buffer.ToArray();
        var contentType = DetectExtension(bytes) switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException("Unsupported image format.")
        };
        return new(bytes, contentType);
    }

    public static string CreateUrl(string contentType)
    {
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Unsupported image format.")
        };
        return UrlPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant() + extension;
    }

    private static string DetectExtension(byte[] bytes)
    {
        ReadOnlySpan<byte> data = bytes;
        if (data.Length >= 4 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff &&
            data[^2] == 0xff && data[^1] == 0xd9) return ".jpg";
        if (data.Length >= 33 && data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            data.Slice(12, 4).SequenceEqual("IHDR"u8) && data[^8..^4].SequenceEqual("IEND"u8)) return ".png";
        if (data.Length >= 20 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8) &&
            BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4)) == data.Length - 8 &&
            (data.Slice(12, 4).SequenceEqual("VP8 "u8) || data.Slice(12, 4).SequenceEqual("VP8L"u8) ||
             data.Slice(12, 4).SequenceEqual("VP8X"u8))) return ".webp";
        throw ApiException.Validation("image", "Only files with JPEG, PNG, or WebP image signatures are accepted.");
    }
}
