using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Greenlight.DocumentProcess.Shared.Ingestion.Classification.Classifiers;
using Microsoft.Greenlight.Shared.Models.Classification;

namespace Microsoft.Greenlight.DocumentProcess.CustomData.Ingestion.Classification.Classifiers;

public class CustomDataDocumentClassifier : IDocumentClassifier
{
    private readonly DocumentAnalysisClient _documentAnalysisClient;

    public CustomDataDocumentClassifier(DocumentAnalysisClient documentAnalysisClient)
    {
        _documentAnalysisClient = documentAnalysisClient;
    }

    public async Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUriWithSasToken,
        string classificationModelName)
    {
        var documentClassificationResult = new DocumentClassificationResult();

        var operation =
            await _documentAnalysisClient.ClassifyDocumentFromUriAsync(WaitUntil.Completed, classificationModelName, new Uri(documentUriWithSasToken));

        if (operation.HasCompleted && operation.Value != null)
        {
            var result = operation.Value;
            var documentClassificationType = result.Documents[0].DocumentType switch
            {
                "er-mixedchaptertitles" => DocumentClassificationType.NrcEnvironmentalReportWithMixedTitles,
                "er-numberedchapters" => DocumentClassificationType.NrcEnvironmentalReportWithNumberedChapters,
                "figures-tables-only" => DocumentClassificationType.NrcFiguresAndTablesOnly,
                "headings-only" => DocumentClassificationType.NrcHeadingsOnly,
                "withheld-section-coverpage" => DocumentClassificationType.NrcWithHeldSectionCover,
            };

            var confidence = result.Documents[0].Confidence;

            documentClassificationResult = new DocumentClassificationResult
            {
                ClassificationShortCode = result.Documents[0].DocumentType,
                Confidence = confidence,
                SuccessfulClassification = true
            };
        }
        else
        {
            documentClassificationResult.SuccessfulClassification = false;
        }

        return documentClassificationResult;
    }
}
