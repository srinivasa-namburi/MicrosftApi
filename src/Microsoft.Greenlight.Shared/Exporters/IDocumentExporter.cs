using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Exporters;

/// <summary>
/// Interface for document exporter.
/// </summary>
public interface IDocumentExporter
{
    /// <summary>
    /// Exports the document asynchronously.
    /// </summary>
    /// <param name="generatedDocument">The generated document.</param>
    /// <param name="documentHasNumbering">Indicates if the document has numbering.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. 
    /// The task result contains the stream of the exported document.
    /// </returns>
    Task<Stream> ExportDocumentAsync(GeneratedDocument generatedDocument, bool documentHasNumbering);

    /// <summary>
    /// Exports the document asynchronously by document ID.
    /// </summary>
    /// <param name="generatedDocumentId">The ID of the generated document.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the stream of the exported document, or null if the document is not found.
    /// </returns>
    Task<Stream?> ExportDocumentAsync(Guid generatedDocumentId);

    /// <summary>
    /// Regular expression for title numbering.
    /// </summary>
    const string TitleNumberingRegex = "[0-9\\.]+ (.*)";
}
