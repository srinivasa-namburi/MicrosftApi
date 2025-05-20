// Copyright (c) Microsoft Corporation. All rights reserved.
using System;
using System.Text.Json;

namespace Microsoft.Greenlight.Grains.Shared.Contracts.Models
{
    /// <summary>
    /// Represents a row in the km-index-us-nrc-envrep-sections table.
    /// </summary>
    public class IndexSectionRow
    {
        public string _pk { get; set; } = string.Empty;
        public float[] embedding { get; set; } = Array.Empty<float>();
        public string[] labels { get; set; } = Array.Empty<string>();
        public string chunk { get; set; } = string.Empty;
        public JsonElement extras { get; set; } // Use JsonElement for jsonb
        public string my_field1 { get; set; } = string.Empty;
        public DateTimeOffset? _update { get; set; }
    }
}
