// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// What to do after processing a streaming update.
    /// </summary>
    public enum StreamingProcessingAction
    {
        Continue,
        Skip,
        JsonErrorContinue
    }
}