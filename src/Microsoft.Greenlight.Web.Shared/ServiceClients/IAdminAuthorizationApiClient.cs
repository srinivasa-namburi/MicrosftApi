// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IAdminAuthorizationApiClient : IServiceClient
{
    Task<List<PermissionInfo>> ListPermissionsAsync();
    Task<List<RoleInfo>> ListRolesAsync();
    Task<RoleInfo> UpsertRoleAsync(UpsertRoleRequest request);
    Task DeleteRoleAsync(Guid roleId);
    Task SetRolePermissionsAsync(SetRolePermissionsRequest request);
    Task AssignUserRoleAsync(AssignUserRoleRequest request);
    Task RemoveAssignmentAsync(string providerSubjectId, Guid roleId);
    Task<List<UserSearchResult>> SearchUsersAsync(string query);
    Task<bool> CanAccessAdminAuthorizationAsync();
    Task<AuthorizationDebugInfo> GetAuthorizationDebugInfoAsync();
}
