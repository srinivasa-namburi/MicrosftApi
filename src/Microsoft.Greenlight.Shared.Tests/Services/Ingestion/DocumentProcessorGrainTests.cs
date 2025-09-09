// Copyright (c) Microsoft Corporation. All rights reserved.
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Ingestion;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Moq;

namespace Microsoft.Greenlight.Shared.Tests.Services.Ingestion;

public class DocumentProcessorGrainTests
{
    private class TestDocIngestionService : IDocumentIngestionService
    {
        public Task ClearIndexAsync(string documentLibraryName, string indexName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<DocumentIngestionResult> DeleteDocumentAsync(string documentLibraryName, string indexName, string fileName)
            => Task.FromResult(DocumentIngestionResult.Ok(0, 0));

        public Task<DocumentIngestionResult> IngestDocumentAsync(
            Guid documentId,
            Stream fileStream,
            string fileName,
            string documentUrl,
            string documentLibraryName,
            string indexName,
            string? userId,
            Dictionary<string, string>? metadata = null)
        {
            return Task.FromResult(DocumentIngestionResult.Ok(3, 42));
        }
    }

    private sealed class SimpleDbContextFactory : IDbContextFactory<DocGenerationDbContext>
    {
        private readonly DbContextOptions<DocGenerationDbContext> _options;
        public SimpleDbContextFactory(DbContextOptions<DocGenerationDbContext> options) => _options = options;
        public DocGenerationDbContext CreateDbContext() => new DocGenerationDbContext(_options);
    }

    private static IDbContextFactory<DocGenerationDbContext> CreateInMemoryDbFactory(out Guid docId)
    {
        var options = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase($"DocProcessor_{Guid.NewGuid()}")
            .Options;
        var factory = new SimpleDbContextFactory(options);
        using var db = factory.CreateDbContext();
        var entity = new Microsoft.Greenlight.Shared.Models.IngestedDocument
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
            DocumentLibraryOrProcessName = "ProcA",
            Container = "container",
            FolderPath = "folder",
            FileName = "file.txt",
            OriginalDocumentUrl = "http://example/blob",
            OrchestrationId = "container/folder",
            IngestionState = IngestionState.Uploaded
        };
        db.IngestedDocuments.Add(entity);
        db.SaveChanges();
        docId = entity.Id;
        return factory;
    }

    [Fact]
    public async Task ProcessDocumentAsync_SetsVectorFields_OnSuccess()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DocumentProcessorGrain>>();
        var docProcessInfo = Mock.Of<IDocumentProcessInfoService>(m =>
            m.GetDocumentProcessInfoByShortNameAsync("ProcA") == Task.FromResult<DocumentProcessInfo?>(new DocumentProcessInfo
            {
                ShortName = "ProcA",
                BlobStorageContainerName = "container",
                Repositories = new List<string> { "indexA" },
                LogicType = DocumentProcessLogicType.SemanticKernelVectorStore
            })
        );
        var services = new ServiceCollection();
        // Minimal DI for AzureFileHelper inside grain
        services.AddLogging();
        services.AddDbContextFactory<DocGenerationDbContext>(o => o.UseInMemoryDatabase($"DPG_{Guid.NewGuid()}"));
        services.AddSingleton<AzureFileHelper>(sp =>
        {
            // Use a stubbed AzureFileHelper that returns an empty stream for GetFileAsStreamFromFullBlobUrlAsync
            var dbf = sp.GetRequiredService<IDbContextFactory<DocGenerationDbContext>>();
            var blobClient = new BlobServiceClient("UseDevelopmentStorage=true");
            var logger = sp.GetRequiredService<ILogger<AzureFileHelper>>();
            return new TestAzureFileHelper(blobClient, dbf, logger);
        });
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var dbFactory = CreateInMemoryDbFactory(out var docId);
        var ingestion = new TestDocIngestionService();
        var fileUrlResolver = new Mock<IFileUrlResolverService>();

        var grain = new DocumentProcessorGrain(
            logger,
            docProcessInfo,
            provider,
            dbFactory,
            ingestion,
            fileUrlResolver.Object);

        // Act
        var result = await grain.ProcessDocumentAsync(docId);

        // Assert
        Assert.True(result.Success);
        await using var db = await dbFactory.CreateDbContextAsync();
        var updated = await db.IngestedDocuments.FindAsync(docId);
        Assert.NotNull(updated);
        Assert.Equal(IngestionState.Complete, updated!.IngestionState);
        Assert.True(updated.IsVectorStoreIndexed);
        Assert.Equal("indexA", updated.VectorStoreIndexName);
        Assert.False(string.IsNullOrWhiteSpace(updated.VectorStoreDocumentId));
        Assert.Equal(3, updated.VectorStoreChunkCount);
    }

    private sealed class TestAzureFileHelper : AzureFileHelper
    {
        public TestAzureFileHelper(BlobServiceClient blobServiceClient,
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<AzureFileHelper> logger) : base(blobServiceClient, dbContextFactory, logger) { }

        public override async Task<Stream?> GetFileAsStreamFromFullBlobUrlAsync(string fullBlobUrl)
        {
            // Return small dummy content
            await Task.Yield();
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes("dummy"));
        }
    }
}
