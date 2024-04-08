// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Classification.Classifiers;
using ProjectVico.V2.Shared.Models.Classification;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Ingestion.Classification.Classifiers;

public class NrcAdamsDocumentClassifier : IDocumentClassifier
{

    private readonly DocumentAnalysisClient _documentAnalysisClient;

    public NrcAdamsDocumentClassifier(DocumentAnalysisClient documentAnalysisClient)
    {
        _documentAnalysisClient = documentAnalysisClient;
    }

    public async Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUriWithSasToken, string classificationModelName)
    {
        var documentClassificationResult = new DocumentClassificationResult();

        // This requires DI being able to access the url which doesn't work when running blob storage simulated locally
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


