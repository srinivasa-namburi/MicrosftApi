// Copyright (c) Microsoft. All rights reserved.

using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;

namespace ProjectVico.Backend.DocumentIngestion.Shared.Pipelines;

public class BaselinePipeline : IPdfPipeline
{

    private readonly AiOptions _aiOptions;
    private readonly IContentTreeProcessor _contentTreeProcessor;
    private readonly IContentTreeJsonTransformer _contentTreeJsonTransformer;

    private const string LineSeparator = "------------------------------------------------------------------";

    public BaselinePipeline(
        IOptions<AiOptions> aiOptions,
        IContentTreeProcessor contentTreeProcessor,
        IContentTreeJsonTransformer contentTreeJsonTransformer)
    {
        this._aiOptions = aiOptions.Value;
        this._contentTreeProcessor = contentTreeProcessor;
        this._contentTreeJsonTransformer = contentTreeJsonTransformer;
    }

    public Task<List<ContentNode>> RunAsync(MemoryStream pdfStream, string pdfName)
    {
        throw new NotImplementedException();
    }
}
