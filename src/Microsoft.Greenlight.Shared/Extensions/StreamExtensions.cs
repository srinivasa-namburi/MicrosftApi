using System.Security.Cryptography;

namespace Microsoft.Greenlight.Shared.Extensions;

public static class StreamExtensions
{
    public static string GenerateHashFromStreamAndResetStream(this Stream stream)
    {
        using var sha256 = SHA256.Create();
        stream.Position = 0;
        var hash = sha256.ComputeHash(stream);
        stream.Position = 0;
        return Convert.ToBase64String(hash);
    }
}
