// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Contracts.Requests.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models.FileStorage;
using Moq;

namespace Microsoft.Greenlight.Shared.Tests.Controllers;

public sealed class FileStorageSourceControllerTests
{
    private static (string databaseName, AutoMapper.IMapper mapper) CreateDatabaseAndMapper()
    {
        var databaseName = $"fssctl_{Guid.NewGuid()}";
        var mc = new AutoMapper.MapperConfiguration(cfg =>
        {
            cfg.AddProfile<FileStorageMappingProfile>();
        });
        return (databaseName, mc.CreateMapper());
    }

    [Fact]
    public async Task CreateAndGet_FileStorageSource_WithCategories_Works()
    {
        var (databaseName, mapper) = CreateDatabaseAndMapper();
        var controller = new FileStorageSourceController(new TestDbFactory(databaseName), mapper, Mock.Of<ILogger<FileStorageSourceController>>());

        // Seed host using a separate context
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;
        
        using (var db = new DocGenerationDbContext(dbOptions))
        {
            var host = new FileStorageHost
            {
                Id = Guid.NewGuid(),
                Name = "Test Host",
                ProviderType = FileStorageProviderType.BlobStorage,
                ConnectionString = "default",
                IsActive = true,
                IsDefault = true
            };
            db.FileStorageHosts.Add(host);
            await db.SaveChangesAsync();

            var create = new CreateFileStorageSourceRequest
            {
                Name = "Source A",
                FileStorageHostId = host.Id,
                ContainerOrPath = "content-references",
                IsActive = true,
                StorageSourceDataTypes = new List<FileStorageSourceDataType> { FileStorageSourceDataType.ContentReference }
            };

            var created = await controller.CreateFileStorageSourceAsync(create);
            var ok = Assert.IsType<OkObjectResult>(created.Result);
            var dto = Assert.IsType<FileStorageSourceInfo>(ok.Value);
            Assert.Contains(FileStorageSourceDataType.ContentReference, dto.StorageSourceDataTypes);

            var get = await controller.GetFileStorageSourceByIdAsync(dto.Id);
            var ok2 = Assert.IsType<OkObjectResult>(get.Result);
            var dto2 = Assert.IsType<FileStorageSourceInfo>(ok2.Value);
            Assert.Single(dto2.StorageSourceDataTypes);
            Assert.Equal(FileStorageSourceDataType.ContentReference, dto2.StorageSourceDataTypes[0]);

            // Update add another role
            var upd = new UpdateFileStorageSourceRequest
            {
                Id = dto.Id,
                Name = dto2.Name,
                FileStorageHostId = dto2.FileStorageHostId,
                ContainerOrPath = dto2.ContainerOrPath,
                IsActive = true,
                StorageSourceDataTypes = new List<FileStorageSourceDataType> { FileStorageSourceDataType.ContentReference, FileStorageSourceDataType.Ingestion }
            };
            var updated = await controller.UpdateFileStorageSourceAsync(dto.Id, upd);
            var ok3 = Assert.IsType<OkObjectResult>(updated.Result);
            var dto3 = Assert.IsType<FileStorageSourceInfo>(ok3.Value);
            Assert.Contains(FileStorageSourceDataType.Ingestion, dto3.StorageSourceDataTypes);
        }
    }

    [Fact]
    public async Task TypeMapping_Create_Update_Delete_Works()
    {
        var (databaseName, mapper) = CreateDatabaseAndMapper();
        var controller = new FileStorageSourceController(new TestDbFactory(databaseName), mapper, Mock.Of<ILogger<FileStorageSourceController>>());
        
        // Seed host + source using a separate context
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;
        
        Guid hostId, srcId;
        using (var db = new DocGenerationDbContext(dbOptions))
        {
            var host = new FileStorageHost
            {
                Id = Guid.NewGuid(), Name = "Host", ProviderType = FileStorageProviderType.BlobStorage, ConnectionString = "default", IsActive = true
            };
            db.FileStorageHosts.Add(host);
            var src = new FileStorageSource
            {
                Id = Guid.NewGuid(), Name = "Src", FileStorageHostId = host.Id, ContainerOrPath = "test", IsActive = true
            };
            db.FileStorageSources.Add(src);
            await db.SaveChangesAsync();
            
            hostId = host.Id;
            srcId = src.Id;
        }

        // Create mapping
        var created = await controller.CreateMappingAsync(ContentReferenceType.ExternalLinkAsset, srcId);
        var ok = Assert.IsType<OkObjectResult>(created.Result);
        var m = Assert.IsType<ContentReferenceTypeStorageSourceMappingInfo>(ok.Value);
        Assert.Equal(srcId, m.FileStorageSourceId);
        Assert.Equal(ContentReferenceType.ExternalLinkAsset, m.ContentReferenceType);

        // Update mapping
        var upd = await controller.UpdateMappingAsync(ContentReferenceType.ExternalLinkAsset, srcId, new FileStorageSourceController.UpdateContentReferenceTypeMappingRequest
        {
            Priority = 5, IsActive = true, AcceptsUploads = true
        });
        var ok2 = Assert.IsType<OkObjectResult>(upd.Result);
        var m2 = Assert.IsType<ContentReferenceTypeStorageSourceMappingInfo>(ok2.Value);
        Assert.Equal(5, m2.Priority);
        Assert.True(m2.AcceptsUploads);

        // Get by type
        var list = await controller.GetMappingsForTypeAsync(ContentReferenceType.ExternalLinkAsset);
        var ok3 = Assert.IsType<OkObjectResult>(list.Result);
        var arr = Assert.IsAssignableFrom<List<ContentReferenceTypeStorageSourceMappingInfo>>(ok3.Value);
        Assert.Single(arr);

        // Delete
        var del = await controller.DeleteMappingAsync(ContentReferenceType.ExternalLinkAsset, srcId);
        Assert.IsType<OkResult>(del);
        var list2 = await controller.GetMappingsForTypeAsync(ContentReferenceType.ExternalLinkAsset);
        var ok4 = Assert.IsType<OkObjectResult>(list2.Result);
        var arr2 = Assert.IsAssignableFrom<List<ContentReferenceTypeStorageSourceMappingInfo>>(ok4.Value);
        Assert.Empty(arr2);
    }

    private sealed class TestDbFactory : IDbContextFactory<DocGenerationDbContext>
    {
        private readonly string _databaseName;
        private readonly DbContextOptions<DocGenerationDbContext> _options;
        
        public TestDbFactory(string databaseName) 
        { 
            _databaseName = databaseName;
            _options = new DbContextOptionsBuilder<DocGenerationDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;
        }
        
        public DocGenerationDbContext CreateDbContext() => new DocGenerationDbContext(_options);
        public Task<DocGenerationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => 
            Task.FromResult(new DocGenerationDbContext(_options));
    }
}

