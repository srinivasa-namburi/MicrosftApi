namespace ProjectVico.V2.Shared.Contracts;

public class RagRepositorySearchResult
{
    public string RepositoryName { get; set; }
    public List<ReportDocument> Documents { get; set; }
}