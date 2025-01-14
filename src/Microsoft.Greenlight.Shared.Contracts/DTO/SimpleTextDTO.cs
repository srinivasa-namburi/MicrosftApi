namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a simple text data transfer object.
/// </summary>
public record SimpleTextDTO()
{
    /// <summary>
    /// Unique identifier for the simple text.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
