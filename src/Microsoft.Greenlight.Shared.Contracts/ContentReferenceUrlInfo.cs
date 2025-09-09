// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts;

/// <summary>
/// DTO representing a resolved URL for a content reference item.
/// </summary>
public class ContentReferenceUrlInfo
{
    /// <summary>
    /// The proxied URL for downloading/opening the content reference.
    /// </summary>
    public required string Url { get; set; }
}

