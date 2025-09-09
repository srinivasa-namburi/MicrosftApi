// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Categorization for a FileStorageSource indicating what it is intended to store.
    /// Defaults to Ingestion for all existing sources to preserve behavior.
    /// </summary>
    public enum FileStorageSourceDataType
    {
        Ingestion = 100,
        ContentReference = 200,
        MediaAssets = 300
    }
}

