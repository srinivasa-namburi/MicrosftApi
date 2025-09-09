// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Moq;
using Xunit.Abstractions;

namespace Microsoft.Greenlight.Shared.Tests.Services.FileStorage;

public class BlobStorageFileStorageServiceTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private BlobServiceClient? _blobServiceClient;
    private string _connectionString = string.Empty;
    private readonly bool _useRealAzurite;

    public BlobStorageFileStorageServiceTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Check if we can use real Azurite (either TestContainers or local installation)
        _useRealAzurite = CanUseRealAzurite();
    }

    public async Task InitializeAsync()
    {
        if (_useRealAzurite)
        {
            await InitializeWithRealAzuriteAsync();
        }
        else
        {
            _output.WriteLine("Skipping blob storage tests - Docker/Azurite not available");
        }
    }

    public async Task DisposeAsync()
    {
        // Nothing to dispose in the simplified version
        await Task.CompletedTask;
    }

    private async Task InitializeWithRealAzuriteAsync()
    {
        try
        {
            // First try TestContainers if Docker is available
            if (IsDockerAvailable())
            {
                var azuriteContainer = new Testcontainers.Azurite.AzuriteBuilder()
                    .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
                    .Build();
                await azuriteContainer.StartAsync();
                _connectionString = azuriteContainer.GetConnectionString();
                _blobServiceClient = new BlobServiceClient(_connectionString);
                _output.WriteLine($"Using TestContainers Azurite: {_connectionString}");
                return;
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"TestContainers failed: {ex.Message}");
        }

        // Fallback to local Azurite if available
        try
        {
            _connectionString = "UseDevelopmentStorage=true";
            _blobServiceClient = new BlobServiceClient(_connectionString);
            
            // Test the connection
            var testContainer = _blobServiceClient.GetBlobContainerClient($"connection-test-{Guid.NewGuid():N}");
            await testContainer.CreateIfNotExistsAsync();
            await testContainer.DeleteIfExistsAsync();
            
            _output.WriteLine("Using local Azurite installation");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Local Azurite failed: {ex.Message}");
            _blobServiceClient = null;
            _connectionString = string.Empty;
        }
    }

    private static bool CanUseRealAzurite()
    {
        return IsDockerAvailable() || IsLocalAzuriteAvailable();
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            
            return process?.WaitForExit(5000) == true && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLocalAzuriteAvailable()
    {
        try
        {
            var client = new BlobServiceClient("UseDevelopmentStorage=true");
            var testTask = client.GetPropertiesAsync();
            testTask.Wait(TimeSpan.FromSeconds(3));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IDbContextFactory<DocGenerationDbContext> CreateInMemoryDbFactory()
    {
        var options = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: $"BlobAckTests_{Guid.NewGuid()}")
            .Options;
        return new TestDbContextFactory(options);
    }

    private FileStorageSourceInfo CreateSourceInfo(string container, bool shouldMove)
    {
        return new FileStorageSourceInfo
        {
            Id = Guid.NewGuid(),
            Name = "test-source",
            ContainerOrPath = container,
            ShouldMoveFiles = shouldMove,
            AutoImportFolderName = "ingest-auto",
            IsActive = true,
            FileStorageHost = new FileStorageHostInfo 
            { 
                ProviderType = Microsoft.Greenlight.Shared.Enums.FileStorageProviderType.BlobStorage, 
                ConnectionString = _connectionString
            },
            FileStorageHostId = Guid.NewGuid()
        };
    }

    [SkippableFact]
    public async Task AcknowledgeFileAsync_ShouldUpsertAck_InAckOnlyMode()
    {
        Skip.IfNot(_useRealAzurite && _blobServiceClient != null, "Real Azurite not available for testing");

        // Arrange
        var container = $"test-ackonly-{Guid.NewGuid():N}";
        var blobService = _blobServiceClient!;
        var dbFactory = CreateInMemoryDbFactory();
        var logger = Mock.Of<ILogger<BlobStorageFileStorageService>>();
        var sourceInfo = CreateSourceInfo(container, shouldMove: false);
        var service = new BlobStorageFileStorageService(blobService, logger, sourceInfo, dbFactory);

        var containerClient = blobService.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync();
        var blobName = $"{(sourceInfo.AutoImportFolderName ?? "ingest-auto")}/{Guid.NewGuid():N}.txt";
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(new BinaryData(Encoding.UTF8.GetBytes("hello")));

        // Act
        var finalUrl = await service.AcknowledgeFileAsync(blobName, targetPath: blobName, default);

        // Assert
        Assert.Equal(blobClient.Uri.ToString(), finalUrl);
        await using var db = await dbFactory.CreateDbContextAsync();
        var ack = await db.FileAcknowledgmentRecords.FirstOrDefaultAsync(x => x.FileStorageSourceId == sourceInfo.Id && x.RelativeFilePath == blobName);
        Assert.NotNull(ack);
        Assert.False(string.IsNullOrWhiteSpace(ack!.FileHash));
        Assert.Equal(blobClient.Uri.ToString(), ack.FileStorageSourceInternalUrl);

        // Source still exists
        Assert.True((await blobClient.ExistsAsync()).Value);
    }

    [SkippableFact]
    public async Task AcknowledgeFileAsync_ShouldCopyDeleteAndAck_InMoveMode()
    {
        Skip.IfNot(_useRealAzurite && _blobServiceClient != null, "Real Azurite not available for testing");

        // Arrange
        var container = $"test-move-{Guid.NewGuid():N}";
        var blobService = _blobServiceClient!;
        var dbFactory = CreateInMemoryDbFactory();
        var logger = Mock.Of<ILogger<BlobStorageFileStorageService>>();
        var sourceInfo = CreateSourceInfo(container, shouldMove: true);
        var service = new BlobStorageFileStorageService(blobService, logger, sourceInfo, dbFactory);

        var containerClient = blobService.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync();
        var sourceName = $"{(sourceInfo.AutoImportFolderName ?? "ingest-auto")}/{Guid.NewGuid():N}.txt";
        var targetName = $"processed/{Guid.NewGuid():N}.txt";
        var sourceBlob = containerClient.GetBlobClient(sourceName);
        var targetBlob = containerClient.GetBlobClient(targetName);
        await sourceBlob.UploadAsync(new BinaryData(Encoding.UTF8.GetBytes("world")));

        // Act
        var finalUrl = await service.AcknowledgeFileAsync(sourceName, targetName, default);

        // Assert
        Assert.Equal(targetBlob.Uri.ToString(), finalUrl);

        await using var db = await dbFactory.CreateDbContextAsync();
        var ack = await db.FileAcknowledgmentRecords.FirstOrDefaultAsync(x => x.FileStorageSourceId == sourceInfo.Id && x.RelativeFilePath == targetName);
        Assert.NotNull(ack);
        Assert.False(string.IsNullOrWhiteSpace(ack!.FileHash));
        Assert.Equal(targetBlob.Uri.ToString(), ack.FileStorageSourceInternalUrl);  // Should store the final URL after move
        Assert.Equal(targetName, ack.RelativeFilePath);  // Should store the final relative path after move

        // Target exists, source deleted
        Assert.True((await targetBlob.ExistsAsync()).Value);
        Assert.False((await sourceBlob.ExistsAsync()).Value);
    }
}

internal sealed class TestDbContextFactory : IDbContextFactory<DocGenerationDbContext>
{
    private readonly DbContextOptions<DocGenerationDbContext> _options;
    public TestDbContextFactory(DbContextOptions<DocGenerationDbContext> options) => _options = options;
    public DocGenerationDbContext CreateDbContext() => new DocGenerationDbContext(_options);
}
