// Copyright (c) Microsoft. All rights reserved.


// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Responses;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Ingestion.Pipelines;


public interface IPdfPipeline
{
    Task<IngestionPipelineResponse> RunAsync(IngestedDocument document, DocumentProcessOptions documentProcessOptions);
}
