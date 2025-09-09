// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;
using Xunit;

namespace Microsoft.Greenlight.Shared.Tests.SystemStatus;

public class SystemStatusEnumsTests
{
    [Fact]
    public void SystemStatusSeverity_HasCorrectValues()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)SystemStatusSeverity.Info);
        Assert.Equal(1, (int)SystemStatusSeverity.Warning);
        Assert.Equal(2, (int)SystemStatusSeverity.Critical);
        
        // Verify all expected values exist
        var values = Enum.GetValues<SystemStatusSeverity>();
        Assert.Equal(3, values.Length);
        Assert.Contains(SystemStatusSeverity.Info, values);
        Assert.Contains(SystemStatusSeverity.Warning, values);
        Assert.Contains(SystemStatusSeverity.Critical, values);
    }

    [Fact]
    public void SystemHealthStatus_HasCorrectValues()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)SystemHealthStatus.Healthy);
        Assert.Equal(1, (int)SystemHealthStatus.Warning);
        Assert.Equal(2, (int)SystemHealthStatus.Critical);
        Assert.Equal(3, (int)SystemHealthStatus.Unknown);
        
        // Verify all expected values exist
        var values = Enum.GetValues<SystemHealthStatus>();
        Assert.Equal(4, values.Length);
        Assert.Contains(SystemHealthStatus.Healthy, values);
        Assert.Contains(SystemHealthStatus.Warning, values);
        Assert.Contains(SystemHealthStatus.Critical, values);
        Assert.Contains(SystemHealthStatus.Unknown, values);
    }

    [Fact]
    public void SystemStatusType_HasCorrectValues()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)SystemStatusType.OperationStarted);
        Assert.Equal(1, (int)SystemStatusType.OperationCompleted);
        Assert.Equal(2, (int)SystemStatusType.OperationFailed);
        Assert.Equal(3, (int)SystemStatusType.ProgressUpdate);
        Assert.Equal(4, (int)SystemStatusType.HealthUpdate);
        Assert.Equal(5, (int)SystemStatusType.WorkerStatus);
        Assert.Equal(6, (int)SystemStatusType.ConfigurationChange);
        Assert.Equal(7, (int)SystemStatusType.ResourceUpdate);
        Assert.Equal(8, (int)SystemStatusType.Information);
        
        // Verify all expected values exist
        var values = Enum.GetValues<SystemStatusType>();
        Assert.Equal(9, values.Length);
        Assert.Contains(SystemStatusType.OperationStarted, values);
        Assert.Contains(SystemStatusType.OperationCompleted, values);
        Assert.Contains(SystemStatusType.OperationFailed, values);
        Assert.Contains(SystemStatusType.ProgressUpdate, values);
        Assert.Contains(SystemStatusType.HealthUpdate, values);
        Assert.Contains(SystemStatusType.WorkerStatus, values);
        Assert.Contains(SystemStatusType.ConfigurationChange, values);
        Assert.Contains(SystemStatusType.ResourceUpdate, values);
        Assert.Contains(SystemStatusType.Information, values);
    }

    [Theory]
    [InlineData(SystemStatusSeverity.Info, "Info")]
    [InlineData(SystemStatusSeverity.Warning, "Warning")]
    [InlineData(SystemStatusSeverity.Critical, "Critical")]
    public void SystemStatusSeverity_ToString_ReturnsExpectedName(SystemStatusSeverity severity, string expectedName)
    {
        // Act
        var result = severity.ToString();

        // Assert
        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData(SystemHealthStatus.Healthy, "Healthy")]
    [InlineData(SystemHealthStatus.Warning, "Warning")]
    [InlineData(SystemHealthStatus.Critical, "Critical")]
    [InlineData(SystemHealthStatus.Unknown, "Unknown")]
    public void SystemHealthStatus_ToString_ReturnsExpectedName(SystemHealthStatus status, string expectedName)
    {
        // Act
        var result = status.ToString();

        // Assert
        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData("Info", SystemStatusSeverity.Info)]
    [InlineData("Warning", SystemStatusSeverity.Warning)]
    [InlineData("Critical", SystemStatusSeverity.Critical)]
    public void SystemStatusSeverity_Parse_ReturnsCorrectValue(string name, SystemStatusSeverity expected)
    {
        // Act
        var result = Enum.Parse<SystemStatusSeverity>(name);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Healthy", SystemHealthStatus.Healthy)]
    [InlineData("Warning", SystemHealthStatus.Warning)]
    [InlineData("Critical", SystemHealthStatus.Critical)]
    [InlineData("Unknown", SystemHealthStatus.Unknown)]
    public void SystemHealthStatus_Parse_ReturnsCorrectValue(string name, SystemHealthStatus expected)
    {
        // Act
        var result = Enum.Parse<SystemHealthStatus>(name);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SystemStatusSeverity_IsOrderedBySeverity()
    {
        // This test ensures the enum values are ordered from least to most severe
        // which is important for comparison operations
        
        // Arrange & Act & Assert
        Assert.True(SystemStatusSeverity.Info < SystemStatusSeverity.Warning);
        Assert.True(SystemStatusSeverity.Warning < SystemStatusSeverity.Critical);
        
        // Verify ordering is correct for collections
        var severities = new[] { SystemStatusSeverity.Critical, SystemStatusSeverity.Info, SystemStatusSeverity.Warning };
        var ordered = severities.OrderBy(s => s).ToArray();
        
        Assert.Equal(SystemStatusSeverity.Info, ordered[0]);
        Assert.Equal(SystemStatusSeverity.Warning, ordered[1]);
        Assert.Equal(SystemStatusSeverity.Critical, ordered[2]);
    }
}