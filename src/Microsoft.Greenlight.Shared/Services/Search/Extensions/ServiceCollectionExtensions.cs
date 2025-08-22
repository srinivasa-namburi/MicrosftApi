// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.Services.Search.Extensions;

/// <summary>
/// Extension methods for configuring document ingestion and processing services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds enhanced document ingestion services with PDF/Office support and semantic chunking.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStoreServices(this IServiceCollection services)
    {
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
        services.AddScoped<TesseractLanguageManager>();
        services.AddScoped<ITextExtractionService, EnhancedTextExtractionService>();
        services.AddScoped<ChunkingService>();
        services.AddScoped<SemanticTextChunkingService>();
        services.AddScoped<ITextChunkingService, ChunkingService>(); // Default remains simple unless overridden in factory
        services.AddScoped<IBatchDocumentIngestionService, BatchDocumentIngestionService>();
        services.AddScoped<IDocumentRepositoryFactory, DocumentRepositoryFactory>();

        return services;
    }

    /// <summary>
    /// Adds a custom text extraction service to the service collection.
    /// </summary>
    /// <typeparam name="TService">The type of the custom text extraction service.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTextExtractionService<TService>(this IServiceCollection services)
        where TService : class, ITextExtractionService
    {
        services.AddScoped<ITextExtractionService, TService>();
        return services;
    }

    /// <summary>
    /// Adds a custom text chunking service to the service collection.
    /// </summary>
    /// <typeparam name="TService">The type of the custom text chunking service.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTextChunkingService<TService>(this IServiceCollection services)
        where TService : class, ITextChunkingService
    {
        services.AddScoped<ITextChunkingService, TService>();
        return services;
    }
}
