// Copyright (c) Microsoft. All rights reserved.


// Copyright (c) Microsoft. All rights reserved.

using ProjectVico.Backend.DocumentIngestion.Shared.Models;

namespace ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;

public interface IPdfPipeline
{
    Task<List<ContentNode>> RunAsync(MemoryStream pdfStream, string pdfName);
}
