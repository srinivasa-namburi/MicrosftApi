// Copyright (c) Microsoft Corporation. All rights reserved.
using System;

namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Response DTO for starting an index export or import job.
    /// </summary>
    public class IndexJobStartedResponse
    {
        /// <summary>
        /// Gets or sets the JobId for the started job.
        /// </summary>
        public Guid JobId { get; set; }
    }
}
