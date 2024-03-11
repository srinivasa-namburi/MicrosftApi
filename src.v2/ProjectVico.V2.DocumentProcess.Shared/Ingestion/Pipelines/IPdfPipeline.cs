// Copyright (c) Microsoft. All rights reserved.


// Copyright (c) Microsoft. All rights reserved.

using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Responses;

namespace ProjectVico.V2.DocumentProcess.Shared.Ingestion.Pipelines;


public interface IPdfPipeline
{
    Task<IngestionPipelineResponse> RunAsync(IngestedDocument document, DocumentProcessOptions documentProcessOptions);
}
