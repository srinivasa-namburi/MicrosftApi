namespace ProjectVico.V2.Shared.Contracts.DTO;

public class StoreDocumentForReviewInstanceRequest
{
    public Guid ReviewInstanceId { get; set; }
    public Stream FileStream { get; set; }
    public string FileName { get; set; }
}