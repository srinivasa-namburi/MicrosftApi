// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.Authorization;

/// <summary>
/// Permission keys used throughout the application for authorization checks.
/// These values must be consistent across all projects.
/// </summary>
public static class PermissionKeys
{
    public const string AlterSystemConfiguration = "AlterSystemConfiguration";
    public const string ManageLlmModelsAndDeployments = "ManageLlmModelsAndDeployments";
    public const string GenerateDocument = "GenerateDocument";
    public const string ManageUsersAndRoles = "ManageUsersAndRoles";
    public const string Chat = "Chat";
    public const string DefineReviews = "DefineReviews";
    public const string ExecuteReviews = "ExecuteReviews";
    public const string AlterDocumentProcessesAndLibraries = "AlterDocumentProcessesAndLibraries";
}