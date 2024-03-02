using ProjectVico.V2.Shared.Classification.Models;

namespace ProjectVico.V2.Shared.Classification.Classifiers;

public interface IDocumentClassifier
{
    Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUri, string classificationModelName);
}