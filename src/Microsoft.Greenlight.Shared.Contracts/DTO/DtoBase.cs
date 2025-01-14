namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Base class for Data Transfer Objects (DTOs).
/// </summary>
public abstract class DtoBase
{
    /// <summary>
    /// Unique identifier for the DTO.
    /// </summary>
    public Guid Id { get; set; }
}
