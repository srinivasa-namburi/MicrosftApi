// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts.Helpers;

/// <summary>
/// Helper for generating deterministic orchestration IDs for document ingestion and reindexing processes.
/// </summary>
public static class IngestionOrchestrationIdHelper
{
    /// <summary>
    /// Generates a deterministic orchestration ID (SHA256 hex string) from a container and folder path.
    /// This method is deprecated - use GenerateOrchestrationIdForDocumentLibrary instead for proper DL/DP isolation.
    /// </summary>
    /// <param name="containerName">The blob container name.</param>
    /// <param name="folderPath">The folder path within the container.</param>
    /// <returns>A SHA256-based string suitable for use as a grain key.</returns>
    [Obsolete("Use GenerateOrchestrationIdForDocumentLibrary for proper DL/DP-specific orchestration IDs")]
    public static string GenerateOrchestrationId(string containerName, string folderPath)
    {
        var input = $"{containerName}:{folderPath}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a deterministic orchestration ID (SHA256 hex string) for a file storage source.
    /// One orchestration per FileStorageSource handles discovery and creates IngestedDocuments for all linked DL/DPs.
    /// This is the efficient approach for large repositories.
    /// </summary>
    /// <param name="fileStorageSourceId">The file storage source ID.</param>
    /// <returns>A SHA256-based string suitable for use as a grain key.</returns>
    public static string GenerateOrchestrationIdForFileStorageSource(Guid fileStorageSourceId)
    {
        var input = $"FileStorageSource:{fileStorageSourceId}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a deterministic orchestration ID (SHA256 hex string) specific to a document library/process and file storage source.
    /// This approach is inefficient for large repositories - use GenerateOrchestrationIdForFileStorageSource instead.
    /// </summary>
    /// <param name="documentLibraryOrProcessId">The document library or process ID.</param>
    /// <param name="documentLibraryType">The type (Additional Document Library or Primary Document Process Library).</param>
    /// <param name="fileStorageSourceId">The file storage source ID.</param>
    /// <returns>A SHA256-based string suitable for use as a grain key.</returns>
    [Obsolete("Use GenerateOrchestrationIdForFileStorageSource for better efficiency with large repositories")]
    public static string GenerateOrchestrationIdForDocumentLibrary(
        Guid documentLibraryOrProcessId,
        Microsoft.Greenlight.Shared.Enums.DocumentLibraryType documentLibraryType,
        Guid fileStorageSourceId)
    {
        var input = $"{documentLibraryOrProcessId}:{documentLibraryType}:{fileStorageSourceId}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a deterministic orchestration ID (SHA256 hex string) specific to a document library/process, container, and folder.
    /// This method is deprecated - use the FileStorageSource ID-based method for better provider compatibility.
    /// </summary>
    /// <param name="documentLibraryOrProcessName">The document library or process short name.</param>
    /// <param name="documentLibraryType">The type (Additional Document Library or Primary Document Process Library).</param>
    /// <param name="containerName">The blob container name.</param>
    /// <param name="folderPath">The folder path within the container.</param>
    /// <returns>A SHA256-based string suitable for use as a grain key.</returns>
    [Obsolete("Use the FileStorageSource ID-based overload for better provider compatibility")]
    public static string GenerateOrchestrationIdForDocumentLibrary(
        string documentLibraryOrProcessName,
        Microsoft.Greenlight.Shared.Enums.DocumentLibraryType documentLibraryType,
        string containerName,
        string folderPath)
    {
        var input = $"{documentLibraryOrProcessName}:{documentLibraryType}:{containerName}:{folderPath}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a deterministic reindex orchestration ID (SHA256 hex string) specific to a document library/process.
    /// This is used for reindexing operations that don't depend on specific file sources.
    /// </summary>
    /// <param name="documentLibraryOrProcessId">The document library or process ID.</param>
    /// <param name="documentLibraryType">The type (Additional Document Library or Primary Document Process Library).</param>
    /// <returns>A SHA256-based string suitable for use as a grain key.</returns>
    public static string GenerateReindexOrchestrationId(
        Guid documentLibraryOrProcessId,
        Microsoft.Greenlight.Shared.Enums.DocumentLibraryType documentLibraryType)
    {
        var input = $"REINDEX:{documentLibraryOrProcessId}:{documentLibraryType}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a deterministic reindex orchestration ID (SHA256 hex string) specific to a document library/process.
    /// This method is deprecated - use the ID-based overload for consistency.
    /// </summary>
    /// <param name="documentLibraryOrProcessName">The document library or process short name.</param>
    /// <param name="documentLibraryType">The type (Additional Document Library or Primary Document Process Library).</param>
    /// <returns>A SHA256-based string suitable for use as a grain key.</returns>
    [Obsolete("Use the ID-based overload for consistency")]
    public static string GenerateReindexOrchestrationId(
        string documentLibraryOrProcessName,
        Microsoft.Greenlight.Shared.Enums.DocumentLibraryType documentLibraryType)
    {
        var input = $"REINDEX:{documentLibraryOrProcessName}:{documentLibraryType}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
