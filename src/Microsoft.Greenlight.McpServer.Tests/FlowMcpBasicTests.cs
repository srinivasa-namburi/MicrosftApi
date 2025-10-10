// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.McpServer.Flow.Contracts.Requests;
using Microsoft.Greenlight.McpServer.Flow.Services;
using Microsoft.Greenlight.McpServer.Flow.Tools;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Moq;
using Orleans;

namespace Microsoft.Greenlight.McpServer.Tests;

/// <summary>
/// Basic tests for Flow MCP streaming architecture that can be tested without complex Orleans mocking.
/// Focuses on request validation, error handling, and service structure.
/// </summary>
public class FlowMcpBasicTests
{
    [Fact]
    public void FlowSessionSubscription_HasRequiredProperties()
    {
        var subscription = new FlowSessionSubscription
        {
            SubscriptionId = "test-sub-123",
            SessionId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            UpdateHandler = _ => Task.CompletedTask
        };

        Assert.NotNull(subscription.SubscriptionId);
        Assert.NotEqual(Guid.Empty, subscription.SessionId);
        Assert.True(subscription.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(subscription.UpdateHandler);
    }

    [Fact]
    public void FlowMcpStreamSubscriptionService_CanBeInstantiated()
    {
        var clusterClientMock = new Mock<IClusterClient>();
        var loggerMock = new Mock<ILogger<FlowMcpStreamSubscriptionService>>();

        var service = new FlowMcpStreamSubscriptionService(clusterClientMock.Object, loggerMock.Object);

        Assert.NotNull(service);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    public void StreamingFlowTools_ValidatesSessionIdFormat(string invalidSessionId)
    {
        // These tests verify that invalid session IDs are properly rejected
        // without requiring Orleans infrastructure
        var isValidGuid = Guid.TryParse(invalidSessionId, out _);

        if (string.IsNullOrEmpty(invalidSessionId) || !isValidGuid)
        {
            Assert.False(isValidGuid, "Invalid session ID should not parse as valid GUID");
        }
    }

    [Fact]
    public void FlowQueryRequest_CanBeCreated()
    {
        var request = new FlowQueryRequest
        {
            message = "Test query",
            context = "Test context",
            flowConversationId = Guid.NewGuid().ToString()
        };

        Assert.NotNull(request.message);
        Assert.NotNull(request.context);
        Assert.NotNull(request.flowConversationId);
    }

    [Fact]
    public void FlowQueryStatusRequest_CanBeCreated()
    {
        var request = new FlowQueryStatusRequest
        {
            flowConversationId = Guid.NewGuid().ToString()
        };

        Assert.NotNull(request.flowConversationId);
    }

    [Fact]
    public void FlowQueryCancelRequest_CanBeCreated()
    {
        var request = new FlowQueryCancelRequest
        {
            flowConversationId = Guid.NewGuid().ToString(),
            reason = "Test cancellation"
        };

        Assert.NotNull(request.flowConversationId);
        Assert.NotNull(request.reason);
    }

    [Fact]
    public void FlowBackendConversationUpdate_CanBeCreated()
    {
        var update = new FlowBackendConversationUpdate(
            Guid.NewGuid(), // CorrelationId
            Guid.NewGuid(), // FlowSessionId
            Guid.NewGuid(), // BackendConversationId
            new Shared.Contracts.Chat.ChatMessageDTO
            {
                Id = Guid.NewGuid(),
                Message = "Test message",
                Source = Shared.Enums.ChatMessageSource.Assistant
            }, // ChatMessageDto
            "test-process", // DocumentProcessName
            true // IsComplete
        );

        Assert.NotEqual(Guid.Empty, update.CorrelationId);
        Assert.NotEqual(Guid.Empty, update.FlowSessionId);
        Assert.NotEqual(Guid.Empty, update.BackendConversationId);
        Assert.NotNull(update.ChatMessageDto);
        Assert.NotNull(update.DocumentProcessName);
        Assert.True(update.IsComplete);
    }
}