// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.FileStorage;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Moq;

namespace Microsoft.Greenlight.Shared.Tests.Services.FileStorage;

public class LocalFileStorageServiceTests
{
    private static IDbContextFactory<DocGenerationDbContext> CreateInMemoryDbFactory()
    {
        var options = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: $"LocalFsTests_{Guid.NewGuid()}")
            .Options;
    return new LocalFsTestDbContextFactory(options);
    }

    private static FileStorageSourceInfo CreateLocalSourceInfo(string baseRoot, bool shouldMove = false, string? containerOrPath = null)
    {
        return new FileStorageSourceInfo
        {
            Id = Guid.NewGuid(),
            Name = "local-test-source",
            ContainerOrPath = containerOrPath ?? string.Empty,
            ShouldMoveFiles = shouldMove,
            AutoImportFolderName = "ingest-auto",
            IsActive = true,
            FileStorageHost = new FileStorageHostInfo
            {
                ProviderType = FileStorageProviderType.LocalFileSystem,
                ConnectionString = baseRoot
            },
            FileStorageHostId = Guid.NewGuid()
        };
    }

    [Fact]
    public async Task AcknowledgeFileAsync_UpsertsAck_And_DoesNotMoveOrDelete()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), $"gl_local_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var dbFactory = CreateInMemoryDbFactory();
            var logger = Mock.Of<ILogger<LocalFileStorageService>>();
            var sourceInfo = CreateLocalSourceInfo(tempRoot, shouldMove: false, containerOrPath: "");
            var service = new LocalFileStorageService(logger, sourceInfo, dbFactory);

            // Create a file under the default auto-import folder
            var rel = $"{service.DefaultAutoImportFolder}/{Guid.NewGuid():N}.txt";
            var full = service.GetFullPath(rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, "hello world");

            // Act
            var returnedFull = await service.AcknowledgeFileAsync(rel, targetPath: rel, default);

            // Assert path is unchanged and file still exists
            Assert.Equal(full, returnedFull);
            Assert.True(File.Exists(full));

            await using var db = await dbFactory.CreateDbContextAsync();
            var ack = await db.FileAcknowledgmentRecords.FirstOrDefaultAsync(x => x.FileStorageSourceId == sourceInfo.Id && x.RelativeFilePath == rel);
            Assert.NotNull(ack);
            Assert.Equal(full, ack!.FileStorageSourceInternalUrl);
            Assert.False(string.IsNullOrWhiteSpace(ack.FileHash));

            // Modify file content and acknowledge again -> hash should update
            await File.WriteAllTextAsync(full, "hello world updated");
            var returnedAgain = await service.AcknowledgeFileAsync(rel, targetPath: rel, default);
            Assert.Equal(full, returnedAgain);

            var ack2 = await db.FileAcknowledgmentRecords.FirstAsync(x => x.FileStorageSourceId == sourceInfo.Id && x.RelativeFilePath == rel);
            Assert.Equal(full, ack2.FileStorageSourceInternalUrl);
            Assert.False(string.IsNullOrWhiteSpace(ack2.FileHash));
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Upload_And_SaveFileInfo_PersistsExternalLinkAsset()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), $"gl_local_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var dbFactory = CreateInMemoryDbFactory();
            var logger = Mock.Of<ILogger<LocalFileStorageService>>();
            var sourceInfo = CreateLocalSourceInfo(tempRoot, shouldMove: false, containerOrPath: "");
            var service = new LocalFileStorageService(logger, sourceInfo, dbFactory);

            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("sample file content"));
            var rel = await service.UploadFileAsync("sample.txt", content);

            // Act
            var result = await service.SaveFileInfoAsync(rel, "sample.txt");

            // Assert
            Assert.Equal(rel, result.RelativePath);
            Assert.True(File.Exists(result.FullPath));
            Assert.NotEqual(Guid.Empty, result.ExternalLinkAssetId);
            Assert.False(string.IsNullOrWhiteSpace(result.FileHash));
            Assert.NotNull(result.AccessUrl);

            await using var db = await dbFactory.CreateDbContextAsync();
            var asset = await db.ExternalLinkAssets.FirstOrDefaultAsync(x => x.Id == result.ExternalLinkAssetId);
            Assert.NotNull(asset);
            Assert.Equal(result.FullPath, asset!.Url);
            Assert.Equal(sourceInfo.Id, asset.FileStorageSourceId);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task EF_Can_Link_Ack_To_Multiple_IngestedDocuments()
    {
        var dbFactory = CreateInMemoryDbFactory();
        await using var db = await dbFactory.CreateDbContextAsync();

        // Arrange: one ack record
        var ack = new FileAcknowledgmentRecord
        {
            Id = Guid.NewGuid(),
            FileStorageSourceId = Guid.NewGuid(),
            RelativeFilePath = "ingest-auto/file1.txt",
            FileStorageSourceInternalUrl = "/tmp/ingest-auto/file1.txt",
            FileHash = "hash-1",
            AcknowledgedDate = DateTime.UtcNow
        };
        db.FileAcknowledgmentRecords.Add(ack);

        // Two ingested documents for different DP/DL identities
        var doc1 = new IngestedDocument
        {
            Id = Guid.NewGuid(),
            FileName = "file1.txt",
            Container = "container-a",
            FolderPath = "ingest-auto",
            OrchestrationId = "orch-a",
            RunId = Guid.NewGuid(),
            IngestionState = IngestionState.Discovered,
            IngestedDate = DateTime.UtcNow,
            DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
            DocumentLibraryOrProcessName = "dp-one",
            FileHash = "hash-1",
            OriginalDocumentUrl = "file:///tmp/ingest-auto/file1.txt"
        };
        var doc2 = new IngestedDocument
        {
            Id = Guid.NewGuid(),
            FileName = "file1.txt",
            Container = "container-a",
            FolderPath = "ingest-auto",
            OrchestrationId = "orch-b",
            RunId = Guid.NewGuid(),
            IngestionState = IngestionState.Discovered,
            IngestedDate = DateTime.UtcNow,
            DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
            DocumentLibraryOrProcessName = "dl-two",
            FileHash = "hash-1",
            OriginalDocumentUrl = "file:///tmp/ingest-auto/file1.txt"
        };
        db.IngestedDocuments.AddRange(doc1, doc2);

        db.IngestedDocumentFileAcknowledgments.Add(new IngestedDocumentFileAcknowledgment
        {
            Id = Guid.NewGuid(),
            IngestedDocumentId = doc1.Id,
            FileAcknowledgmentRecordId = ack.Id
        });
        db.IngestedDocumentFileAcknowledgments.Add(new IngestedDocumentFileAcknowledgment
        {
            Id = Guid.NewGuid(),
            IngestedDocumentId = doc2.Id,
            FileAcknowledgmentRecordId = ack.Id
        });

        await db.SaveChangesAsync();

        // Act: load with navigation
        var loadedAck = await db.FileAcknowledgmentRecords
            .Include(x => x.IngestedDocumentLinks)
            .FirstAsync(x => x.Id == ack.Id);

        // Assert: both links present
        Assert.Equal(2, loadedAck.IngestedDocumentLinks.Count);
        Assert.Contains(loadedAck.IngestedDocumentLinks, l => l.IngestedDocumentId == doc1.Id);
        Assert.Contains(loadedAck.IngestedDocumentLinks, l => l.IngestedDocumentId == doc2.Id);
    }
}

internal sealed class LocalFsTestDbContextFactory : IDbContextFactory<DocGenerationDbContext>
{
    private readonly DbContextOptions<DocGenerationDbContext> _options;
    public LocalFsTestDbContextFactory(DbContextOptions<DocGenerationDbContext> options) => _options = options;
    public DocGenerationDbContext CreateDbContext() => new DocGenerationDbContext(_options);
}
