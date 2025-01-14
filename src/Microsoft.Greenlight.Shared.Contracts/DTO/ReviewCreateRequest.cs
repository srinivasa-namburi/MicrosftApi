namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a request to create a review.
/// </summary>
public class ReviewCreateRequest
{
    /// <summary>
    /// Review definition information.
    /// </summary>
    public required ReviewDefinitionInfo ReviewDefinition { get; set; }
}
