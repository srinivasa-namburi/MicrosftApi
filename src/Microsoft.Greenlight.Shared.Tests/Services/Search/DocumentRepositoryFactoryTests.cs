// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Moq;

namespace Microsoft.Greenlight.Shared.Tests.Services.Search;

/// <summary>
/// Tests for <see cref="DocumentRepositoryFactory"/>.
/// </summary>
public class DocumentRepositoryFactoryTests
{
    /// <summary>
    /// Verifies that a Kernel Memory logic type returns the legacy adapter (kept for backward compatibility).
    /// </summary>
    [Fact]
    public async Task CreateForDocumentProcessAsync_KernelMemory_ReturnsAdapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Register minimal dependencies
        var kmRepo = new Mock<IKernelMemoryRepository>().Object;
        services.AddSingleton(kmRepo);

        var docLibInfoService = new Mock<IDocumentLibraryInfoService>();
        services.AddSingleton(docLibInfoService.Object);

        // Real EF Core in-memory DbContextFactory
        services.AddDbContextFactory<DocGenerationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<DocumentRepositoryFactory>>();
        var factory = new DocumentRepositoryFactory(provider, docLibInfoService.Object, logger);

        var process = new DocumentProcessInfo
        {
            ShortName = "testlib",
            BlobStorageContainerName = "container",
            LogicType = DocumentProcessLogicType.KernelMemory
        };

        // Act
        var repo = await factory.CreateForDocumentProcessAsync(process);

        // Assert
#pragma warning disable CS0618 // Suppress obsolete warning for intentional adapter validation
        Assert.IsType<KernelMemoryDocumentRepositoryAdapter>(repo);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Verifies that a SemanticKernelVectorStore logic type returns the SemanticKernelVectorStoreRepository.
    /// </summary>
    [Fact]
    public async Task CreateForDocumentProcessAsync_SemanticKernelVectorStore_ReturnsRepository()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Required services for SemanticKernelVectorStoreRepository
        var mockEmbeddingService = new Mock<IAiEmbeddingService>();
        var mockProvider = new Mock<ISemanticKernelVectorStoreProvider>();
        var mockTextExtractionService = new Mock<ITextExtractionService>();
        var docLibInfoService = new Mock<IDocumentLibraryInfoService>();

        services.AddSingleton(mockEmbeddingService.Object);
        services.AddSingleton(mockProvider.Object);
        services.AddSingleton(mockTextExtractionService.Object);
        services.AddSingleton(docLibInfoService.Object);

        // Register concrete ChunkingService (factory expects concrete type)
        services.AddSingleton<ChunkingService>();

        // Real EF Core in-memory DbContextFactory so factory can query models safely
        services.AddDbContextFactory<DocGenerationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Configuration options required by factory/repository
        services.Configure<Microsoft.Greenlight.Shared.Configuration.ServiceConfigurationOptions>(options =>
        {
            options.GreenlightServices = new Microsoft.Greenlight.Shared.Configuration.ServiceConfigurationOptions.GreenlightServicesOptions
            {
                VectorStore = new Microsoft.Greenlight.Shared.Configuration.VectorStoreOptions
                {
                    ChunkSize = 1000,
                    ChunkOverlap = 100
                }
            };
        });

        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<DocumentRepositoryFactory>>();
        var factory = new DocumentRepositoryFactory(provider, docLibInfoService.Object, logger);

        var process = new DocumentProcessInfo
        {
            ShortName = "testlib",
            BlobStorageContainerName = "container",
            LogicType = DocumentProcessLogicType.SemanticKernelVectorStore
        };

        // Act
        var repo = await factory.CreateForDocumentProcessAsync(process);

        // Assert
        Assert.IsType<SemanticKernelVectorStoreRepository>(repo);
    }

    /// <summary>
    /// Verifies that a Classic logic type throws NotSupportedException.
    /// </summary>
    [Fact]
    public async Task CreateForDocumentProcessAsync_Classic_ThrowsNotSupportedException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var docLibInfoService = new Mock<IDocumentLibraryInfoService>();
        services.AddSingleton(docLibInfoService.Object);

        // Real EF Core in-memory DbContextFactory (factory constructor resolves it)
        services.AddDbContextFactory<DocGenerationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<DocumentRepositoryFactory>>();
        var factory = new DocumentRepositoryFactory(provider, docLibInfoService.Object, logger);

        var process = new DocumentProcessInfo
        {
            ShortName = "testlib",
            BlobStorageContainerName = "container",
            LogicType = DocumentProcessLogicType.Classic
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => 
            factory.CreateForDocumentProcessAsync(process));
    }

    /// <summary>
    /// Verifies that unknown logic type throws ArgumentException.
    /// </summary>
    [Fact]
    public async Task CreateForDocumentProcessAsync_Unknown_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var docLibInfoService = new Mock<IDocumentLibraryInfoService>();
        services.AddSingleton(docLibInfoService.Object);

        // Real EF Core in-memory DbContextFactory (factory constructor resolves it)
        services.AddDbContextFactory<DocGenerationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<DocumentRepositoryFactory>>();
        var factory = new DocumentRepositoryFactory(provider, docLibInfoService.Object, logger);

        var process = new DocumentProcessInfo
        {
            ShortName = "testlib",
            BlobStorageContainerName = "container",
            LogicType = (DocumentProcessLogicType)999 // Invalid enum value
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            factory.CreateForDocumentProcessAsync(process));
    }

    /// <summary>
    /// Verifies that CreateForDocumentLibraryAsync falls back to Kernel Memory when library not found.
    /// </summary>
    [Fact]
    public async Task CreateForDocumentLibraryAsync_NotFound_ReturnsKernelMemoryRepository()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var kmRepo = new Mock<IKernelMemoryRepository>().Object;
        services.AddSingleton(kmRepo);

        var docLibInfoService = new Mock<IDocumentLibraryInfoService>();
        docLibInfoService.Setup(x => x.GetDocumentLibraryByShortNameAsync("nonexistent"))
            .ReturnsAsync((Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary.DocumentLibraryInfo?)null);
        services.AddSingleton(docLibInfoService.Object);

        // Real EF Core in-memory DbContextFactory (factory constructor resolves it)
        services.AddDbContextFactory<DocGenerationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<DocumentRepositoryFactory>>();
        var factory = new DocumentRepositoryFactory(provider, docLibInfoService.Object, logger);

        // Act
        var repo = await factory.CreateForDocumentLibraryAsync("nonexistent");

        // Assert
#pragma warning disable CS0618 // Suppress obsolete warning for intentional adapter validation
        Assert.IsType<KernelMemoryDocumentRepositoryAdapter>(repo);
#pragma warning restore CS0618
    }
}
