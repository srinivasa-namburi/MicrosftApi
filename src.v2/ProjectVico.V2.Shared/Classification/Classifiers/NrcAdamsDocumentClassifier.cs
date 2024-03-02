// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Storage.Blobs;
using ProjectVico.V2.Shared.Classification.Models;
using ProjectVico.V2.Shared.Helpers;

namespace ProjectVico.V2.Shared.Classification.Classifiers;

public class NrcAdamsDocumentClassifier : IDocumentClassifier
{

    private readonly DocumentAnalysisClient _documentAnalysisClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureFileHelper _fileHelper;

    public NrcAdamsDocumentClassifier(DocumentAnalysisClient documentAnalysisClient, BlobServiceClient blobServiceClient, AzureFileHelper fileHelper)
    {
        _documentAnalysisClient = documentAnalysisClient;
        _blobServiceClient = blobServiceClient;
        _fileHelper = fileHelper;
    }

    public async Task<DocumentClassificationResult> ClassifyDocumentFromUri(string documentUriWithSasToken, string classificationModelName)
    {
        var documentClassificationResult = new DocumentClassificationResult();

        //TODO: Check if we're running in dev mode and use stream only for local development

        //var fileStream = await _fileHelper.GetFileAsStreamFromFullBlobUrl(documentUriWithSasToken);
        //await using var memoryStream = new MemoryStream();
        //await fileStream.CopyToAsync(memoryStream);
        //memoryStream.Position = 0;

        //var operation =
        //    await _documentAnalysisClient.ClassifyDocumentAsync(WaitUntil.Completed, classificationModelName,
        //        memoryStream);

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


