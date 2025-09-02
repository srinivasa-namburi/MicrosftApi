using Orleans;

namespace Microsoft.Greenlight.Shared.Models.Authorization;

/// <summary>
/// Represents a permission that can be assigned to roles.
/// </summary>
[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public sealed class GreenlightPermission
{
    public Guid Id { get; set; }

    /// <summary>
    /// Unique, stable key used in policies, e.g. "AlterSystemConfiguration".
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Human-friendly display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
