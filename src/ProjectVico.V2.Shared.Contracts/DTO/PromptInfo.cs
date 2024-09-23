namespace ProjectVico.V2.Shared.Contracts.DTO
{
    public record PromptInfo
    {
        public Guid? Id { get; set; }
        public Guid DefinitionId { get; set; }
        public required string ShortCode { get; set; }
        public string? Description { get; set; }
        public required string Text { get; set; }
        public Guid? DocumentProcessId { get; set; }
        public string? DocumentProcessName { get; set; }
    }
}