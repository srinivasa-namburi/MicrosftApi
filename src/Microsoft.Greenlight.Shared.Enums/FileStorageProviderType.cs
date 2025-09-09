// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents the type of file storage provider.
/// </summary>
public enum FileStorageProviderType
{
    /// <summary>
    /// Azure Blob Storage provider.
    /// </summary>
    BlobStorage = 0,

    /// <summary>
    /// Local file system provider.
    /// </summary>
    LocalFileSystem = 1,

    /// <summary>
    /// SharePoint provider (reserved for future implementation).
    /// </summary>
    SharePoint = 2
}