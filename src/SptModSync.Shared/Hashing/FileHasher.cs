using System.IO.Hashing;

namespace SptModSync.Shared.Hashing;

public static class FileHasher
{
    private const int BufferSize = 1024 * 1024;

    public static string HashFile(string absolutePath)
    {
        using var stream = new FileStream(
            absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: false);
        var hasher = new XxHash128();
        var buffer = new byte[BufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            hasher.Append(buffer.AsSpan(0, read));
        }

        return ToHexLower(hasher.GetCurrentHash());
    }

    public static async Task<string> HashFileAsync(string absolutePath, CancellationToken ct = default)
    {
        using var stream = new FileStream(
            absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hasher = new XxHash128();
        var buffer = new byte[BufferSize];
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            hasher.Append(buffer.AsSpan(0, read));
        }

        return ToHexLower(hasher.GetCurrentHash());
    }

    private static string ToHexLower(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i * 2] = GetHexChar(b >> 4);
            chars[i * 2 + 1] = GetHexChar(b & 0xF);
        }

        return new string(chars);
    }

    private static char GetHexChar(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + (nibble - 10));
}
