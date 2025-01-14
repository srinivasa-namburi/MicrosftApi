using System.Security.Cryptography;

namespace Microsoft.Greenlight.Shared.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="Stream"/> class.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// Generates a SHA-256 hash from the stream and resets the stream position to the beginning.
    /// </summary>
    /// <param name="stream">The stream to generate the hash from.</param>
    /// <returns>A base64 encoded string representing the SHA-256 hash of the stream.</returns>
    public static string GenerateHashFromStreamAndResetStream(this Stream stream)
    {
        using var sha256 = SHA256.Create();
        stream.Position = 0;
        var hash = sha256.ComputeHash(stream);
        stream.Position = 0;
        return Convert.ToBase64String(hash);
    }
}
