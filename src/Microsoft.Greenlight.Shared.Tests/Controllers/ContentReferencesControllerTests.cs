// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Moq;
using Orleans;

namespace Microsoft.Greenlight.Shared.Tests.Controllers;

public sealed class ContentReferencesControllerTests
{
    [Fact]
    public async Task GetContentReferenceUrl_Returns_Ok_WhenResolverReturnsUrl()
    {
        var mockCr = new Mock<IContentReferenceService>();
        var mockLogger = new Mock<ILogger<ContentReferencesController>>();
        var db = new DocGenerationDbContext(new DbContextOptionsBuilder<DocGenerationDbContext>().UseInMemoryDatabase($"crurlctl_{Guid.NewGuid()}").Options);
        var mockCluster = new Mock<IClusterClient>();
        var mockResolver = new Mock<IFileUrlResolverService>();
        mockResolver.Setup(r => r.ResolveUrlForContentReferenceAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync("/api/file/download/external-asset/abc");

        var controller = new ContentReferencesController(
            mockCr.Object,
            mockLogger.Object,
            db,
            mockCluster.Object,
            mockResolver.Object);

        var res = await controller.GetContentReferenceUrl(Guid.NewGuid());
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var dto = Assert.IsAssignableFrom<Microsoft.Greenlight.Shared.Contracts.ContentReferenceUrlInfo>(ok.Value);
        Assert.Equal("/api/file/download/external-asset/abc", dto.Url);
    }

    [Fact]
    public async Task GetContentReferenceUrl_Returns_NotFound_WhenNull()
    {
        var controller = new ContentReferencesController(
            Mock.Of<IContentReferenceService>(),
            Mock.Of<ILogger<ContentReferencesController>>(),
            new DocGenerationDbContext(new DbContextOptionsBuilder<DocGenerationDbContext>().UseInMemoryDatabase($"crurlctl_{Guid.NewGuid()}").Options),
            Mock.Of<IClusterClient>(),
            Mock.Of<IFileUrlResolverService>());

        var res = await controller.GetContentReferenceUrl(Guid.NewGuid());
        Assert.IsType<NotFoundResult>(res.Result);
    }
}
