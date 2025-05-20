// Copyright (c) Microsoft Corporation. All rights reserved.
using System;
using System.Collections.Generic;

namespace Microsoft.Greenlight.Grains.Document.Contracts.State
{
    /// <summary>
    /// State for ContentStateGrain. Stores content blocks by sequence number.
    /// </summary>
    public class ContentStateGrainState
    {
        /// <summary>
        /// Sequence number to content mapping.
        /// </summary>
        public Dictionary<int, string> DocumentParts { get; set; } = new();

        /// <summary>
        /// Source documents string (for context).
        /// </summary>
        public string SourceDocuments { get; set; } = string.Empty;

        /// <summary>
        /// Block size for sequence increments.
        /// </summary>
        public int BlockSize { get; set; } = 100;
    }
}
