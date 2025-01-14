namespace Microsoft.Greenlight.Shared.Contracts;

/// <summary>
/// Represents the result of a search in the RAG repository.
/// </summary>
public class RagRepositorySearchResult
{
    /// <summary>
    /// Name of the repository.
    /// </summary>
    public string RepositoryName { get; set; }

    /// <summary>
    /// List of documents found in the repository.
    /// </summary>
    public List<ReportDocument> Documents { get; set; }
}
