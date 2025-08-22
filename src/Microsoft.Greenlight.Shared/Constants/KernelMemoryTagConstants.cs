// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Constants;

/// <summary>
/// Constants for Kernel Memory reserved tag names used in searches and lookups.
/// </summary>
public static class KernelMemoryTagConstants
{
    /// <summary>
    /// Reserved tag added automatically by Kernel Memory to each partition indicating its numeric order.
    /// NOTE: Value chosen based on Kernel Memory conventions; adjust if upstream package changes.
    /// </summary>
    public const string FilePartitionNumberTag = "file_partition_number";
}
