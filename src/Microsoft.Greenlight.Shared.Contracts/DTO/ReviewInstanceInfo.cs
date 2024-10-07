using Microsoft.Greenlight.Shared.Enums;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public record ReviewInstanceInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewDefinitionId { get; set; }
    
    public Guid ExportedLinkId { get; set; }
    public string? ReviewDefinitionStateWhenSubmitted { get; set; }
    
    public ReviewInstanceStatus Status { get; set; } = ReviewInstanceStatus.Pending;
}
