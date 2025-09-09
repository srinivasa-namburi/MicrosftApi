// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.FileStorage;
using Microsoft.Greenlight.Shared.Services.Caching;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Moq;

namespace Microsoft.Greenlight.Shared.Tests.Services.FileStorage;

public sealed class FileUrlResolverServiceContentReferenceTests
{
    [Fact]
    public async Task ResolveUrlForContentReferenceAsync_Returns_ProxiedAssetUrl_WhenAckLinked()
    {
        // Arrange in-memory DB with CR->Ack->Source
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase($"crurl_{Guid.NewGuid()}")
            .Options;
        var dbFactory = new TestDbFactory(dbOptions);
        var sourceId = Guid.NewGuid();
        var crId = Guid.NewGuid();
        await using (var db = new DocGenerationDbContext(dbOptions))
        {
            db.FileStorageSources.Add(new FileStorageSource { Id = sourceId, Name = "x", FileStorageHostId = Guid.NewGuid(), ContainerOrPath = "cont", IsActive = true });
            var ack = new FileAcknowledgmentRecord { Id = Guid.NewGuid(), FileStorageSourceId = sourceId, RelativeFilePath = "rel", FileStorageSourceInternalUrl = "full", FileHash = "h", AcknowledgedDate = DateTime.UtcNow };
            db.FileAcknowledgmentRecords.Add(ack);
            db.ContentReferenceItems.Add(new ContentReferenceItem { Id = crId, ReferenceType = ContentReferenceType.ExternalFile, FileHash = "h" });
            db.Add(new ContentReferenceFileAcknowledgment { Id = Guid.NewGuid(), ContentReferenceItemId = crId, FileAcknowledgmentRecordId = ack.Id });
            await db.SaveChangesAsync();
        }

        var cache = new NoOpAppCache();
        var resolver = new FileUrlResolverService(dbFactory, Mock.Of<ILogger<FileUrlResolverService>>(), cache);

        // Act
        var url = await resolver.ResolveUrlForContentReferenceAsync(crId);

        // Assert (proxied route)
        Assert.NotNull(url);
        Assert.StartsWith("/api/file/download/external-asset/", url);

        //Dispose the in-memory DB
        await using (var db = new DocGenerationDbContext(dbOptions))
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestDbFactory(DbContextOptions<DocGenerationDbContext> o)
        : IDbContextFactory<DocGenerationDbContext>
    {
        public DocGenerationDbContext CreateDbContext() => new DocGenerationDbContext(o);
    }
}

