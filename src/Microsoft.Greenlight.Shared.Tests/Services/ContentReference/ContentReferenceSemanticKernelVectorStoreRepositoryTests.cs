// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.FileStorage;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Moq;

namespace Microsoft.Greenlight.Shared.Tests.Services.ContentReference;

public sealed class ContentReferenceSemanticKernelVectorStoreRepositoryTests
{
    [Fact]
    public async Task IndexAsync_TagsContain_FileStorageSourceId_WhenLinked()
    {
        // Arrange: in-memory DB with CR -> Ack -> Source linkage
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase($"crrepo_{Guid.NewGuid()}")
            .Options;
        var dbFactory = new SimpleDbFactory(dbOptions);
        Guid sourceId = Guid.NewGuid();
        Guid crId = Guid.NewGuid();

        await using (var db = new DocGenerationDbContext(dbOptions))
        {
            db.FileStorageSources.Add(new FileStorageSource
            {
                Id = sourceId,
                Name = "test",
                FileStorageHostId = Guid.NewGuid(),
                ContainerOrPath = "container",
                IsActive = true
            });
            var ack = new FileAcknowledgmentRecord
            {
                Id = Guid.NewGuid(),
                FileStorageSourceId = sourceId,
                RelativeFilePath = "rel",
                FileStorageSourceInternalUrl = "full",
                FileHash = "hash",
                AcknowledgedDate = DateTime.UtcNow
            };
            db.FileAcknowledgmentRecords.Add(ack);
            db.ContentReferenceItems.Add(new ContentReferenceItem
            {
                Id = crId,
                ReferenceType = ContentReferenceType.ExternalFile,
                FileHash = "hash",
                RagText = "some text"
            });
            db.Add(new ContentReferenceFileAcknowledgment
            {
                Id = Guid.NewGuid(),
                ContentReferenceItemId = crId,
                FileAcknowledgmentRecordId = ack.Id
            });
            await db.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<ContentReferenceSemanticKernelVectorStoreRepository>>();
        var mockEmbedding = new Mock<IAiEmbeddingService>();
        mockEmbedding.Setup(x => x.ResolveEmbeddingConfigForContentReferenceTypeAsync(It.IsAny<ContentReferenceType>()))
            .ReturnsAsync(("emb-deploy", 3));
        mockEmbedding.Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<string>(), "emb-deploy", 3))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        var mockProvider = new Mock<ISemanticKernelVectorStoreProvider>();
        List<SkVectorChunkRecord> captured = new();
        mockProvider.Setup(x => x.UpsertAsync(It.IsAny<string>(), It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<SkVectorChunkRecord>, CancellationToken>((_, recs, __) => captured.AddRange(recs))
            .Returns(Task.CompletedTask);

        var mockChunker = new Mock<ITextChunkingService>();
        mockChunker.Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<string> { "chunk-1" });

        var options = new ServiceConfigurationOptions
        {
            GreenlightServices = new ServiceConfigurationOptions.GreenlightServicesOptions
            {
                VectorStore = new VectorStoreOptions { ChunkSize = 1000, ChunkOverlap = 0, VectorSize = 3, MaxChunkTextLength = 8000 }
            }
        };
        var mockOpts = new Mock<IOptionsMonitor<ServiceConfigurationOptions>>();
        mockOpts.Setup(x => x.CurrentValue).Returns(options);

        var mockCrService = new Mock<IContentReferenceService>();
        mockCrService.Setup(x => x.GetContentTextForContentReferenceItem(It.IsAny<ContentReferenceItem>()))
            .ReturnsAsync((ContentReferenceItem r) => r.RagText);

        var repo = new ContentReferenceSemanticKernelVectorStoreRepository(
            mockLogger.Object,
            mockEmbedding.Object,
            mockProvider.Object,
            mockChunker.Object,
            mockOpts.Object,
            dbFactory,
            mockCrService.Object);

        // Act
        await repo.IndexAsync(new ContentReferenceItem { Id = crId, ReferenceType = ContentReferenceType.ExternalFile, RagText = "abc" });

        // Assert
        Assert.NotEmpty(captured);
        var tags = captured[0].Tags;
        Assert.True(tags.ContainsKey("fileStorageSourceId"));
        Assert.Contains(sourceId.ToString(), tags["fileStorageSourceId"]);
    }

    private sealed class SimpleDbFactory : IDbContextFactory<DocGenerationDbContext>
    {
        private readonly DbContextOptions<DocGenerationDbContext> _o;
        public SimpleDbFactory(DbContextOptions<DocGenerationDbContext> o) { _o = o; }
        public DocGenerationDbContext CreateDbContext() => new DocGenerationDbContext(_o);
    }
}

