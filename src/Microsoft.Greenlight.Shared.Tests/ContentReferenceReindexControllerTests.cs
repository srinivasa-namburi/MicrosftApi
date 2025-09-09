// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.State;
using Microsoft.Greenlight.Shared.Enums;
using Moq;
using Orleans;
using Xunit;

namespace Microsoft.Greenlight.Shared.Tests;

/// <summary>
/// API controller tests for ContentReferenceReindexController.
/// </summary>
public class ContentReferenceReindexControllerTests
{
    [Fact]
    public async Task Start_ReturnsAccepted_AndInvokesGrain()
    {
        var type = ContentReferenceType.ExternalLinkAsset;
        var orchestrationId = $"cr-reindex-{type}";

        var grain = new Mock<IContentReferenceReindexOrchestrationGrain>();
        grain.Setup(g => g.StartReindexingAsync(type, It.IsAny<string>()))
             .Returns(Task.CompletedTask)
             .Verifiable();

        var client = new Mock<IClusterClient>();
        client.Setup(c => c.GetGrain<IContentReferenceReindexOrchestrationGrain>(orchestrationId, null))
              .Returns(grain.Object);

        var controller = new ContentReferenceReindexController(client.Object, Mock.Of<ILogger<ContentReferenceReindexController>>());

        var result = await controller.Start(type, "Unit test");
        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(accepted.Value);
        Assert.Contains(orchestrationId, accepted.Value!.ToString());
        grain.Verify();
    }

    [Fact]
    public async Task GetState_ReturnsOk_WithGrainState()
    {
        var type = ContentReferenceType.GeneratedDocument;
        var orchestrationId = $"cr-reindex-{type}";
        var expected = new ContentReferenceReindexState
        {
            Id = orchestrationId,
            ReferenceType = type,
            Total = 10,
            Processed = 3,
            Failed = 1,
            Running = true
        };

        var grain = new Mock<IContentReferenceReindexOrchestrationGrain>();
        grain.Setup(g => g.GetStateAsync())
             .ReturnsAsync(expected);

        var client = new Mock<IClusterClient>();
        client.Setup(c => c.GetGrain<IContentReferenceReindexOrchestrationGrain>(orchestrationId, null))
              .Returns(grain.Object);

        var controller = new ContentReferenceReindexController(client.Object, Mock.Of<ILogger<ContentReferenceReindexController>>());

        var result = await controller.GetState(type) as OkObjectResult;
        Assert.NotNull(result);
        var state = Assert.IsType<ContentReferenceReindexState>(result!.Value);
        Assert.Equal(expected.Id, state.Id);
        Assert.Equal(expected.Total, state.Total);
        Assert.True(state.Running);
    }
}

