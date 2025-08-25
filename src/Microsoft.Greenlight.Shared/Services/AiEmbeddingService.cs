// Copyright (c) Microsoft Corporation. All rights reserved.
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using OpenAI.Embeddings;

namespace Microsoft.Greenlight.Shared.Services;

#pragma warning disable SKEXP0001
/// <summary>
/// Service for generating embeddings using Azure OpenAI and Semantic Kernel
/// </summary>
public class AiEmbeddingService : IAiEmbeddingService
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly ILogger<AiEmbeddingService> _logger;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;

    /// <summary>
    /// Constructs a new instance of the AiEmbeddingService
    /// </summary>
    public AiEmbeddingService(
        [FromKeyedServices("openai-planner")] 
        AzureOpenAIClient openAIClient,
        IOptionsSnapshot<ServiceConfigurationOptions> serviceConfigurationOptions,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        ILogger<AiEmbeddingService> logger)
    {
        _openAIClient = openAIClient;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingsAsync(string text)
    {
        return await GenerateEmbeddingsAsync(text, _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName, null);
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingsAsync(string text, string deploymentName, int? dimensions)
    {
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Empty text provided for embeddings generation");
            return Array.Empty<float>();
        }

        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            deploymentName = _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName;
        }

        // Trim text if it's too long (most embedding models have token limits)
        if (text.Length > 16384)
        {
            _logger.LogWarning("Text truncated to 16384 characters for embedding generation");
            text = text.Substring(0, 16384);
        }

        try
        {
            var embeddingClient = _openAIClient.GetEmbeddingClient(deploymentName);
            var options = new EmbeddingGenerationOptions { EndUserId = "system" };
            if (dimensions.HasValue && dimensions.Value > 0)
            {
                options.Dimensions = dimensions.Value;
            }

            var embeddingResult = await embeddingClient.GenerateEmbeddingAsync(text, options);
            return embeddingResult.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings for deployment {Deployment}", deploymentName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingsForDocumentProcessAsync(string documentProcessShortName, string text)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var dp = await db.DynamicDocumentProcessDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ShortName == documentProcessShortName);
        if (dp == null)
        {
            // Fallback: sometimes callers pass a library short name; try library as well.
            var lib = await db.DocumentLibraries.AsNoTracking().FirstOrDefaultAsync(l => l.ShortName == documentProcessShortName);
            if (lib != null)
            {
                return await GenerateEmbeddingsForDocumentLibraryAsync(documentProcessShortName, text);
            }

            _logger.LogWarning("Document Process {DP} not found; using global embedding deployment", documentProcessShortName);
            return await GenerateEmbeddingsAsync(text);
        }

        if (dp.LogicType != DocumentProcessLogicType.SemanticKernelVectorStore)
        {
            // Kernel Memory or other logic -> use global default
            return await GenerateEmbeddingsAsync(text);
        }

        // Resolve deployment name and dimensionality
        string deploymentName;
        if (dp.EmbeddingModelDeploymentId.HasValue)
        {
            var dep = await db.AiModelDeployments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dp.EmbeddingModelDeploymentId.Value);
            deploymentName = dep?.DeploymentName ?? _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName;
        }
        else
        {
            deploymentName = _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName;
        }

        int? dims = dp.EmbeddingDimensionsOverride;
        return await GenerateEmbeddingsAsync(text, deploymentName, dims);
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingsForDocumentLibraryAsync(string documentLibraryShortName, string text)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var lib = await db.DocumentLibraries.AsNoTracking()
            .FirstOrDefaultAsync(l => l.ShortName == documentLibraryShortName);
        if (lib == null)
        {
            _logger.LogWarning("Document Library {Lib} not found; using global embedding deployment", documentLibraryShortName);
            return await GenerateEmbeddingsAsync(text);
        }

        if (lib.LogicType != DocumentProcessLogicType.SemanticKernelVectorStore)
        {
            // Kernel Memory or other logic -> use global default
            return await GenerateEmbeddingsAsync(text);
        }

        // Resolve deployment name and dimensionality
        string deploymentName;
        if (lib.EmbeddingModelDeploymentId.HasValue)
        {
            var dep = await db.AiModelDeployments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == lib.EmbeddingModelDeploymentId.Value);
            deploymentName = dep?.DeploymentName ?? _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName;
        }
        else
        {
            deploymentName = _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName;
        }

        int? dims = lib.EmbeddingDimensionsOverride;
        return await GenerateEmbeddingsAsync(text, deploymentName, dims);
    }

    /// <inheritdoc />
    public float CalculateCosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException("Vectors must have the same dimensions");
        }

        float dotProduct = 0;
        float mag1 = 0;
        float mag2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            mag1 += vector1[i] * vector1[i];
            mag2 += vector2[i] * vector2[i];
        }

        if (mag1 == 0 || mag2 == 0)
        {
            return 0;
        }

        return dotProduct / (float)Math.Sqrt(mag1 * mag2);
    }

    /// <summary>
    /// Resolves the effective embedding deployment and vector dimensions for a document process.
    /// </summary>
    public async Task<(string DeploymentName, int Dimensions)> ResolveEmbeddingConfigForDocumentProcessAsync(string documentProcessShortName)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var dp = await db.DynamicDocumentProcessDefinitions.AsNoTracking().FirstOrDefaultAsync(p => p.ShortName == documentProcessShortName);

        if (dp == null)
        {
            // If not a DP, try library of same name
            var lib = await db.DocumentLibraries.AsNoTracking().FirstOrDefaultAsync(l => l.ShortName == documentProcessShortName);
            if (lib != null)
            {
                return await ResolveEmbeddingConfigForDocumentLibraryAsync(documentProcessShortName);
            }

            // Fallback to global
            var defaultDims = _serviceConfigurationOptions.GreenlightServices.VectorStore.VectorSize > 0
                ? _serviceConfigurationOptions.GreenlightServices.VectorStore.VectorSize
                : 1536;
            var defaultDeployment = _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName;
            return (defaultDeployment, defaultDims);
        }

        if (dp.LogicType != DocumentProcessLogicType.SemanticKernelVectorStore)
        {
            var effectiveDimsNonSk = _serviceConfigurationOptions.GreenlightServices.VectorStore.VectorSize > 0
                ? _serviceConfigurationOptions.GreenlightServices.VectorStore.VectorSize
                : 1536;
            return (_serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName, effectiveDimsNonSk);
        }

        string deploymentName = _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName;
        int? dimsOverride = dp.EmbeddingDimensionsOverride;

        if (dp.EmbeddingModelDeploymentId.HasValue)
        {
            var dep = await db.AiModelDeployments
                .Include(d => d.AiModel)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dp.EmbeddingModelDeploymentId.Value);
            if (dep != null)
            {
                deploymentName = dep.DeploymentName;
                if (!dimsOverride.HasValue)
                {
                    dimsOverride = dep.AiModel?.EmbeddingSettings?.Dimensions;
                }
            }
        }

        int effectiveDims = dimsOverride ?? (_serviceConfigurationOptions.GreenlightServices.VectorStore.VectorSize > 0
            ? _serviceConfigurationOptions.GreenlightServices.VectorStore.VectorSize
            : 1536);
        return (deploymentName, effectiveDims);
    }

    /// <summary>
    /// Resolves the effective embedding deployment and vector dimensions for a document library.
    /// </summary>
    public async Task<(string DeploymentName, int Dimensions)> ResolveEmbeddingConfigForDocumentLibraryAsync(string documentLibraryShortName)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var lib = await db.DocumentLibraries.AsNoTracking().FirstOrDefaultAsync(l => l.ShortName == documentLibraryShortName);
        var defaultDims = _serviceConfigurationOptions.GreenlightServices.VectorStore.VectorSize > 0
            ? _serviceConfigurationOptions.GreenlightServices.VectorStore.VectorSize
            : 1536;
        var defaultDeployment = _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName;

        if (lib == null)
        {
            return (defaultDeployment, defaultDims);
        }

        if (lib.LogicType != DocumentProcessLogicType.SemanticKernelVectorStore)
        {
            return (defaultDeployment, defaultDims);
        }

        string deploymentName = defaultDeployment;
        int? dimsOverride = lib.EmbeddingDimensionsOverride;

        if (lib.EmbeddingModelDeploymentId.HasValue)
        {
            var dep = await db.AiModelDeployments
                .Include(d => d.AiModel)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == lib.EmbeddingModelDeploymentId.Value);
            if (dep != null)
            {
                deploymentName = dep.DeploymentName;
                if (!dimsOverride.HasValue)
                {
                    dimsOverride = dep.AiModel?.EmbeddingSettings?.Dimensions;
                }
            }
        }

        int effectiveDims = dimsOverride ?? defaultDims;
        return (deploymentName, effectiveDims);
    }
}
