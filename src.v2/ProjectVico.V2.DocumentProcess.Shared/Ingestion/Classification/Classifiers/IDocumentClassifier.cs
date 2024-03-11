using ProjectVico.V2.Shared.Models.Classification;

namespace ProjectVico.V2.DocumentProcess.Shared.Ingestion.Classification.Classifiers;

public interface IDocumentClassifier
{
    Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUri, string classificationModelName);
}