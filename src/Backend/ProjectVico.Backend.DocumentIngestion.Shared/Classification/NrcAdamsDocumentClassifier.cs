// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using ProjectVico.Backend.DocumentIngestion.Shared.Classification.Models;

namespace ProjectVico.Backend.DocumentIngestion.Shared.Classification;

public interface IDocumentClassifier
{
    Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUri, string classificationModelName);
}

public class CustomDataDocumentClassifier : IDocumentClassifier
{
    private readonly DocumentAnalysisClient _documentAnalysisClient;

    public CustomDataDocumentClassifier(DocumentAnalysisClient documentAnalysisClient)
    {
        this._documentAnalysisClient = documentAnalysisClient;
    }

    public async Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUriWithSasToken,
        string classificationModelName)
    {
        var documentClassificationResult = new DocumentClassificationResult();

        var operation =
            await this._documentAnalysisClient.ClassifyDocumentFromUriAsync(WaitUntil.Completed, classificationModelName, new Uri(documentUriWithSasToken));

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
                ClassificationType = documentClassificationType,
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
public class NrcAdamsDocumentClassifier : IDocumentClassifier
{

    private readonly DocumentAnalysisClient _documentAnalysisClient;

    public NrcAdamsDocumentClassifier(DocumentAnalysisClient documentAnalysisClient)
    {
        this._documentAnalysisClient = documentAnalysisClient;
    }

    public async Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUriWithSasToken, string classificationModelName)
    {
        var documentClassificationResult = new DocumentClassificationResult();

        var operation =
            await this._documentAnalysisClient.ClassifyDocumentFromUriAsync(WaitUntil.Completed, classificationModelName, new Uri(documentUriWithSasToken));

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
                ClassificationType = documentClassificationType,
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


