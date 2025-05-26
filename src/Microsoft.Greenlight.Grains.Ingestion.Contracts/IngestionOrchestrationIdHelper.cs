// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts.Helpers;

/// <summary>
/// Helper for generating deterministic orchestration IDs for document ingestion processes.
/// </summary>
public static class IngestionOrchestrationIdHelper
{
    /// <summary>
    /// Generates a deterministic orchestration ID (SHA256 hex string) from a container and folder path.
    /// </summary>
    /// <param name="containerName">The blob container name.</param>
    /// <param name="folderPath">The folder path within the container.</param>
    /// <returns>A SHA256-based string suitable for use as a grain key.</returns>
    public static string GenerateOrchestrationId(string containerName, string folderPath)
    {
        var input = $"{containerName}:{folderPath}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
