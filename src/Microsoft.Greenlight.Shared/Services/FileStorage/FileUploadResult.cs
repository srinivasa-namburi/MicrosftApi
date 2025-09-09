// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Services.FileStorage;

/// <summary>
/// Represents the result of a file upload operation.
/// </summary>
public class FileUploadResult
{
    /// <summary>
    /// The relative path of the uploaded file within the storage system.
    /// </summary>
    public required string RelativePath { get; set; }

    /// <summary>
    /// The full URL or path for accessing the file externally.
    /// </summary>
    public required string FullPath { get; set; }

    /// <summary>
    /// The access URL for downloading the file through the application's proxy.
    /// </summary>
    public required string AccessUrl { get; set; }

    /// <summary>
    /// Unique identifier of the external link asset record in the database.
    /// </summary>
    public required Guid ExternalLinkAssetId { get; set; }

    /// <summary>
    /// Hash of the uploaded file content for deduplication purposes.
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// Size of the uploaded file in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// MIME type of the uploaded file.
    /// </summary>
    public string? ContentType { get; set; }
}