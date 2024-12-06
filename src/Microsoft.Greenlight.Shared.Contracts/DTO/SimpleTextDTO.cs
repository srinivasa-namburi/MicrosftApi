namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public record SimpleTextDTO()
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
}