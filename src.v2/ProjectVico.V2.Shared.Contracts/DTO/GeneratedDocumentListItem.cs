namespace ProjectVico.V2.Shared.Contracts.DTO;

public class GeneratedDocumentListItem : DtoBase
{
    public string Title { get; set; }
    public DateTime GeneratedDate { get; set; }
    public Guid RequestingAuthorOid { get; set; }
}