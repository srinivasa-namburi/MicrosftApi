using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
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

    /// <summary>
    /// Constructs a new instance of the AiEmbeddingService
    /// </summary>
    public AiEmbeddingService(
        [FromKeyedServices("openai-planner")] 
        AzureOpenAIClient openAIClient,
        IOptionsSnapshot<ServiceConfigurationOptions> serviceConfigurationOptions,
        ILogger<AiEmbeddingService> logger)
    {
        _openAIClient = openAIClient;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingsAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Empty text provided for embeddings generation");
            return Array.Empty<float>();
        }

        // Trim text if it's too long (most embedding models have token limits)
        if (text.Length > 32768)
        {
            _logger.LogWarning("Text truncated to 32768 characters for embedding generation");
            text = text.Substring(0, 32768);
        }


        // We use the OpenAi Client directly to generate embeddings
        try
        {
            var embeddingClient = _openAIClient.GetEmbeddingClient(
                _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName);

            var embeddingResult = await embeddingClient.GenerateEmbeddingAsync(
                text,
                new EmbeddingGenerationOptions { EndUserId = "system" });

            return embeddingResult.Value.ToFloats().ToArray();
        }
        catch (Exception fallbackEx)
        {
            _logger.LogError(fallbackEx, "Error generating embeddings with OpenAI Client method");
            throw;
        }

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
            return 0;

        return dotProduct / (float)Math.Sqrt(mag1 * mag2);
    }
}
