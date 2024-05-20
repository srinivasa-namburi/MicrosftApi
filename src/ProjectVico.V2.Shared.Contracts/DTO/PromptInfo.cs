namespace ProjectVico.V2.Shared.Contracts.DTO
{
    public record PromptInfo
    {
        public Guid Id { get; set; }
        public string ShortCode { get; set; }
        public string Description { get; set; }
        public string Text { get; set; }
        public Guid DocumentProcessId { get; set; }
    }
}