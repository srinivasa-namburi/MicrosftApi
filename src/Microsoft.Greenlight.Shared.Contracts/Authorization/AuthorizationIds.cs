// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.Authorization;

/// <summary>
/// Fixed GUIDs for authorization entities to ensure deterministic IDs across environments.
/// These IDs must never change once released to production.
/// </summary>
public static class AuthorizationIds
{
    /// <summary>
    /// Fixed permission IDs.
    /// </summary>
    public static class Permissions
    {
        // NOTE: These GUIDs are fixed and must not change once released.
        public static readonly Guid AlterSystemConfiguration = new("8d9f2f7e-3a68-4a9a-8d7a-8c1d6b7a3d11");
        public static readonly Guid ManageLlmModelsAndDeployments = new("5e1d5c93-1b18-4b1b-8a6d-3d4a8d7f2c23");
        public static readonly Guid GenerateDocument = new("f3a1c4b5-6d78-4e9a-9b0c-2d1e3f4a5b6c");
        public static readonly Guid ManageUsersAndRoles = new("2a7c9e5d-8b1f-4c3e-9a0d-1b2c3d4e5f60");

        // Added 09/01/25
        public static readonly Guid Chat = new("25c4e90b-f4b1-489e-b00b-5c6845c8b979");
        public static readonly Guid DefineReviews = new("2a393e8a-9841-4924-82f1-010d07534cfc");
        public static readonly Guid ExecuteReviews = new("044e4d1b-300f-4c0b-9fb4-a6cc0a17bb8e");
        public static readonly Guid AlterDocumentProcessesAndLibraries = new("8100832f-e6a7-45f4-9ce8-2637774f4e38");
    }

    /// <summary>
    /// Fixed role IDs.
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// Built-in role that aggregates all permissions.
        /// </summary>
        public static readonly Guid FullAccess = new("1c2d3e4f-5a6b-7c8d-9e0f-102132435465");
    }
}