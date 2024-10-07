using Microsoft.Greenlight.Shared.Models.Classification;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Ingestion.Classification.Classifiers;

public interface IDocumentClassifier
{
    Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUri, string classificationModelName);
}
