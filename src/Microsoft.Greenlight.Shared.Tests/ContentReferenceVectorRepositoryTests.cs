// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Services.Search;
using Moq;
using Xunit;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Microsoft.Greenlight.Shared.Tests;

/// <summary>
/// Tests for ContentReferenceSemanticKernelVectorStoreRepository covering index mapping,
/// upsert/delete flows, and reindex-all behavior.
/// </summary>
public class ContentReferenceVectorRepositoryTests
{
    private static (ContentReferenceSemanticKernelVectorStoreRepository Repo,
                   Mock<ISemanticKernelVectorStoreProvider> Provider,
                   string DatabaseName)
        CreateRepository(ContentReferenceType type, string content = "Hello world", int dims = 1536)
    {
        var logger = Mock.Of<ILogger<ContentReferenceSemanticKernelVectorStoreRepository>>();

        // Mock embedding service -> returns tuple (deployment, dims) and vector for text
        var embeddingService = new Mock<IAiEmbeddingService>();
        embeddingService
            .Setup(s => s.ResolveEmbeddingConfigForContentReferenceTypeAsync(type))
            .ReturnsAsync((DeploymentName: "test-embed", Dimensions: dims));
        embeddingService
            .Setup(s => s.GenerateEmbeddingsAsync(It.IsAny<string>(), "test-embed", dims))
            .ReturnsAsync(Enumerable.Repeat(0.1f, dims).ToArray());

        // Mock provider (Ensure/Upsert/Delete/Clear)
        var provider = new Mock<ISemanticKernelVectorStoreProvider>(MockBehavior.Strict);
        provider.Setup(p => p.EnsureCollectionAsync(It.IsAny<string>(), dims, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        provider.Setup(p => p.UpsertAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        provider.Setup(p => p.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        provider.Setup(p => p.ClearCollectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        // Chunker returns 2 chunks deterministically
        var chunker = new Mock<ITextChunkingService>();
        chunker.Setup(c => c.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
               .Returns(new List<string> { content[..Math.Min(5, content.Length)], content });

        // Options
        var options = Options.Create(new ServiceConfigurationOptions
        {
            GreenlightServices = new ServiceConfigurationOptions.GreenlightServicesOptions
            {
                VectorStore = new VectorStoreOptions
                {
                    ChunkSize = 1000,
                    ChunkOverlap = 100,
                    MaxChunkTextLength = 2000
                }
            }
        });
        var optionsMonitor = Mock.Of<IOptionsMonitor<ServiceConfigurationOptions>>(_ => _.CurrentValue == options.Value);

        // In-memory EF - Create a unique database name for each test
        var databaseName = $"cr_repo_tests_{Guid.NewGuid()}";
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;

        // Mock DbContextFactory to return new instances each time
        var dbFactoryMock = new Mock<IDbContextFactory<DocGenerationDbContext>>();
        dbFactoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => new DocGenerationDbContext(dbOptions));

        // Content reference service to resolve text if missing
        var crService = new Mock<IContentReferenceService>();
        crService.Setup(s => s.GetContentTextForContentReferenceItem(It.IsAny<ContentReferenceItem>())).ReturnsAsync(content);

        var repo = new ContentReferenceSemanticKernelVectorStoreRepository(
            logger,
            embeddingService.Object,
            provider.Object,
            chunker.Object,
            optionsMonitor,
            dbFactoryMock.Object,
            crService.Object);

        return (repo, provider, databaseName);
    }

    [Fact]
    public async Task IndexAsync_EnsuresCollection_UpsertsAndTracks()
    {
        var type = ContentReferenceType.ExternalFile;
        var (repo, provider, databaseName) = CreateRepository(type);
        var item = new ContentReferenceItem
        {
            Id = Guid.NewGuid(),
            ReferenceType = type,
            DisplayName = "X",
            RagText = "Sample content"
        };

        await repo.IndexAsync(item);

        // Provider interactions
        provider.Verify(p => p.EnsureCollectionAsync(It.Is<string>(ix => ix.Contains("system-")), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.UpsertAsync(It.Is<string>(ix => ix.Contains("system-")), It.IsAny<IReadOnlyCollection<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify tracking row exists by creating a new DbContext instance to check
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;
        using var db = new DocGenerationDbContext(dbOptions);
        var track = db.Set<ContentReferenceVectorDocument>().SingleOrDefault();
        Assert.NotNull(track);
        Assert.Equal(item.Id, track!.ContentReferenceItemId);
        Assert.True(track.IsIndexed);
        Assert.True(track.ChunkCount > 0);
        Assert.False(string.IsNullOrWhiteSpace(track.VectorStoreIndexName));
    }

    [Fact]
    public async Task DeleteAsync_DeletesFromProvider_AndRemovesTracking()
    {
        var type = ContentReferenceType.GeneratedSection;
        var (repo, provider, databaseName) = CreateRepository(type);
        var id = Guid.NewGuid();
        var expectedFileName = $"{type}-{id}";

        // Seed tracking data using a separate DbContext instance
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;
        using (var db = new DocGenerationDbContext(dbOptions))
        {
            db.Set<ContentReferenceVectorDocument>().Add(new ContentReferenceVectorDocument
            {
                ContentReferenceItemId = id,
                ReferenceType = type,
                VectorStoreIndexName = SystemIndexes.GeneratedSectionContentReferenceIndex, // Use the actual constant
                VectorStoreDocumentId = $"cr-{id}",
                ChunkCount = 2,
                IndexedUtc = DateTime.UtcNow,
                IsIndexed = true
            });
            await db.SaveChangesAsync();
        }

        await repo.DeleteAsync(id, type);

        provider.Verify(p => p.DeleteFileAsync(It.Is<string>(ix => ix.Contains("system-")), It.Is<string>(fn => fn == expectedFileName), It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify deletion using a new DbContext instance
        using var dbCheck = new DocGenerationDbContext(dbOptions);
        Assert.Empty(dbCheck.Set<ContentReferenceVectorDocument>().ToList());
    }

    [Fact]
    public async Task ReindexAllAsync_ClearsCollection_AndReindexesAllOfType()
    {
        var type = ContentReferenceType.ReviewItem;
        var (repo, provider, databaseName) = CreateRepository(type);

        // Seed two references of same type and another of different type using a separate DbContext instance
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;
        var r1 = new ContentReferenceItem { Id = Guid.NewGuid(), ReferenceType = type, DisplayName = "A", RagText = "A" };
        var r2 = new ContentReferenceItem { Id = Guid.NewGuid(), ReferenceType = type, DisplayName = "B", RagText = "B" };
        var rOther = new ContentReferenceItem { Id = Guid.NewGuid(), ReferenceType = ContentReferenceType.GeneratedDocument, DisplayName = "C", RagText = "C" };
        
        using (var db = new DocGenerationDbContext(dbOptions))
        {
            db.ContentReferenceItems.AddRange(r1, r2, rOther);
            await db.SaveChangesAsync();
        }

        await repo.ReindexAllAsync(type);

        provider.Verify(p => p.ClearCollectionAsync(It.Is<string>(ix => ix.Contains("system-")), It.IsAny<CancellationToken>()), Times.Once);

        // Verify upsert for r1
        var expectedFileName1 = $"{type}-{r1.Id}";
        provider.Verify(p => p.UpsertAsync(
            It.IsAny<string>(),
            It.Is<IReadOnlyCollection<SkVectorChunkRecord>>(chunks => chunks.All(c => c.FileName == expectedFileName1)),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify upsert for r2
        var expectedFileName2 = $"{type}-{r2.Id}";
        provider.Verify(p => p.UpsertAsync(
            It.IsAny<string>(),
            It.Is<IReadOnlyCollection<SkVectorChunkRecord>>(chunks => chunks.All(c => c.FileName == expectedFileName2)),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify total upsert calls
        provider.Verify(p => p.UpsertAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
