// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;

/// <summary>
/// DTO representing association between a ContentReferenceType and a FileStorageSource.
/// </summary>
public class ContentReferenceTypeStorageSourceMappingInfo
{
    public Guid Id { get; set; }
    public ContentReferenceType ContentReferenceType { get; set; }
    public Guid FileStorageSourceId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public bool AcceptsUploads { get; set; }

    public FileStorageSourceInfo? Source { get; set; }
}

