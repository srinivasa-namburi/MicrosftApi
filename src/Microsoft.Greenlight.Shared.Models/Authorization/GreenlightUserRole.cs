using Orleans;

namespace Microsoft.Greenlight.Shared.Models.Authorization;

/// <summary>
/// Assignment of a Greenlight role to a user (by ProviderSubjectId), with source tracking.
/// </summary>
[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public sealed class GreenlightUserRole
{
    public Guid Id { get; set; }

    /// <summary>
    /// Maps to UserInformation.ProviderSubjectId (OID/sub) so we don't need global directory scanning.
    /// </summary>
    public required string ProviderSubjectId { get; set; }

    public Guid RoleId { get; set; }

    /// <summary>
    /// If true, this role membership came from Entra App Role mapping (and must be kept in sync).
    /// If false, it's locally assigned and may later be superseded by Entra.
    /// </summary>
    public bool IsFromEntra { get; set; }

    public DateTime AssignedUtc { get; set; } = DateTime.UtcNow;
}
