// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;

namespace ProjectVico.Backend.DocumentIngestion.Shared.Pipelines;
public class ProductSpecificInputPipeline : IPdfPipeline
{
    private readonly AiOptions _aiOptions;
    private readonly IContentTreeProcessor _contentTreeProcessor;
    private readonly IContentTreeJsonTransformer _contentTreeJsonTransformer;

    private const string LineSeparator = "------------------------------------------------------------------";

    public ProductSpecificInputPipeline(
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
