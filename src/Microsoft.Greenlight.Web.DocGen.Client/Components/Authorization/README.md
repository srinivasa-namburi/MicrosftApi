# Permission-Based UI Authorization System

This document describes the comprehensive permission system implemented for the Greenlight UI, providing dynamic navigation menu updates and granular permission checking throughout the application.

## Overview

The system consists of several key components that work together to provide seamless permission-based UI control:

### Key Components

1. **IPermissionService / PermissionService** - Client-side permission checking with caching
2. **INavMenuStateService / NavMenuStateService** - Dynamic navigation menu state management
3. **PermissionView** - Unified Razor component for conditional rendering based on permissions with multiple modes
4. **AuthorizedNavLink** - Permission-aware navigation link component

## Architecture

### Permission Flow
```
User Authentication ? Permission Cache ? UI Component ? Permission Check ? Render/Hide/Disable
```

### Key Features

- **Cached Permission Lookups**: 10-minute cache with fallback to 1-minute on errors
- **Dynamic NavMenu Updates**: Automatic refresh when document processes change
- **Granular UI Control**: Component-level permission checking with multiple modes
- **Fail-Safe Design**: Denies access on errors, never grants unexpected permissions

## Components

### PermissionView (Unified Component)

The main component for conditionally rendering content based on user permissions. Supports multiple modes for different authorization scenarios.

**Modes:**
- `ShowHide` (default): Show content when authorized, hide when not
- `DisableControls`: Show content but disable controls when not authorized
- `PreserveLayout`: Show content when authorized, preserve space when not (invisible placeholder)

```razor
<!-- Basic usage (ShowHide mode) -->
<PermissionView Permission="@PermissionKeys.GenerateDocument">
    <MudButton>Create Document</MudButton>
</PermissionView>

<!-- Multiple permissions (ANY) -->
<PermissionView AnyPermissions="@(new[] { PermissionKeys.DefineReviews, PermissionKeys.ExecuteReviews })">
    <MudNavLink Href="/reviews">Reviews</MudNavLink>
</PermissionView>

<!-- Multiple permissions (ALL) -->
<PermissionView AllPermissions="@(new[] { PermissionKeys.AlterSystemConfiguration, PermissionKeys.ManageUsersAndRoles })">
    <AdminControls />
</PermissionView>

<!-- DisableControls mode - shows content but disables it when not authorized -->
<PermissionView Permission="@PermissionKeys.AlterSystemConfiguration" 
                Mode="PermissionMode.DisableControls"
                ContainerClass="admin-section">
    <MudButton>Delete Everything</MudButton>
    <MudTextField Label="Critical Setting" />
</PermissionView>

<!-- PreserveLayout mode - maintains layout space -->
<PermissionView Permission="@PermissionKeys.GenerateDocument" 
                Mode="PermissionMode.PreserveLayout">
    <MudButton>Generate</MudButton>
</PermissionView>

<!-- Custom content for unauthorized users -->
<PermissionView Permission="@PermissionKeys.AlterSystemConfiguration">
    <ChildContent>
        <AdminControls />
    </ChildContent>
    <NotAuthorized>
        <MudAlert Severity="Severity.Warning">You need admin permissions to access this.</MudAlert>
    </NotAuthorized>
</PermissionView>
```

#### Parameters
- `Permission` - Single permission key required
- `AnyPermissions` - Array of permissions (user needs ANY)
- `AllPermissions` - Array of permissions (user needs ALL)
- `Mode` - How to handle unauthorized access (ShowHide, DisableControls, PreserveLayout)
- `ChildContent` - Content to show when authorized
- `NotAuthorized` - Content to show when not authorized (ShowHide mode only)
- `Authorizing` - Content to show while checking permissions
- `ContainerClass` - CSS class for container in DisableControls/PreserveLayout modes
- `AdditionalAttributes` - Additional attributes for container element

### DisableControls Mode Details

When using `PermissionMode.DisableControls`, the component:
- Wraps content in a container with reduced opacity (0.6)
- Adds `pointer-events: none` to prevent interaction
- Sets `aria-disabled="true"` for accessibility
- Uses cascading parameters to communicate authorization state to child components
- Provides visual feedback that controls are disabled due to permissions

### AuthorizedNavLink

Permission-aware navigation link that only renders if user has required permissions.

```razor
<AuthorizedNavLink Permission="@PermissionKeys.AlterDocumentProcessesAndLibraries"
                   Href="/document-processes"
                   Icon="@Icons.Material.Filled.Build">
    Document Processes
</AuthorizedNavLink>
```

## Permission Keys

The following permissions are available (from `Microsoft.Greenlight.Shared.Contracts.Authorization.PermissionKeys`):

| Permission Key | Description | Used For |
|---------------|-------------|----------|
| `AlterSystemConfiguration` | System-level configuration changes | Configuration, MCP Servers, System Admin |
| `ManageLlmModelsAndDeployments` | AI model management | Model deployments, AI configuration |
| `GenerateDocument` | Document generation capabilities | Document creation, viewing, deletion |
| `ManageUsersAndRoles` | User and role management | Authorization admin |
| `Chat` | Chat capabilities | Chat interface and conversation management |
| `DefineReviews` | Review definition management | Creating and managing review templates |
| `ExecuteReviews` | Review execution | Performing reviews and accessing results |
| `AlterDocumentProcessesAndLibraries` | Document workflow management | Document processes and libraries |

## Authorization Constants

Authorization constants are centralized in the `Microsoft.Greenlight.Shared.Contracts` project:

- **PermissionKeys**: String constants for permission checking
- **AuthorizationIds**: Fixed GUIDs for permissions and roles

This ensures consistency across all projects and eliminates duplication.

## Best Practices

1. **Use PermissionView for all permission-based UI decisions**
2. **Choose the appropriate mode based on UX requirements**:
   - Use `ShowHide` for optional features
   - Use `DisableControls` when you want to show what's available but restrict access
   - Use `PreserveLayout` when hiding content would break the visual layout
3. **Combine permissions logically** using `AnyPermissions` or `AllPermissions`
4. **Provide meaningful `NotAuthorized` content** when appropriate
5. **Test with different permission levels** to ensure proper behavior

## Performance Considerations

- Permissions are cached for 10 minutes on the client side
- Permission checks are batched where possible
- Failed permission checks are cached for 1 minute to prevent repeated failures
- The NavMenu automatically refreshes when underlying data changes