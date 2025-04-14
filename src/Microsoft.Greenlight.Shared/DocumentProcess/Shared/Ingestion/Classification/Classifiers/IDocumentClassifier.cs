using Microsoft.Greenlight.Shared.Models.Classification;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Ingestion.Classification.Classifiers;

public interface IDocumentClassifier
{
    Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUri, string classificationModelName);
}
