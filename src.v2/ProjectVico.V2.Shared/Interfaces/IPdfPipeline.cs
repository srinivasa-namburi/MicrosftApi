// Copyright (c) Microsoft. All rights reserved.


// Copyright (c) Microsoft. All rights reserved.

using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Responses;

namespace ProjectVico.V2.Shared.Interfaces;


public interface IPdfPipeline
{
    Task<IngestionPipelineResponse> RunAsync(string blobUrl, string pdfName);
}
