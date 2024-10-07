using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Exporters;

public interface IDocumentExporter
{
    Task<Stream> ExportDocumentAsync(GeneratedDocument generatedDocument, bool documentHasNumbering);
    Task<Stream?> ExportDocumentAsync(Guid generatedDocumentId);

    public const string TitleNumberingRegex = "[0-9\\.]+ (.*)";
}
