// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Classification;
using ProjectVico.Backend.DocumentIngestion.Shared.Classification.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Pipelines;

namespace ProjectVico.Backend.DocumentIngestion.API.Functions;

public class IngestPdf
{
    private readonly AiOptions _aiOptions;
    private readonly IOptions<AiOptions> _aiOptionsOptionsContainer;
    private readonly IngestionOptions _ingestionOptions;
    private readonly IOptions<IngestionOptions> _ingestionOptionsContainer;
    private readonly IContentTreeJsonTransformer _jsonTransformer;
    private readonly IContentTreeProcessor _contentTreeProcessor;

    private IPdfPipeline _pdfPipeline;
    private readonly IIndexingProcessor _indexingProcessor;
    private readonly IDocumentClassifier _documentClassifier;

    private const string BlobStorageConnectionStringKeyName = "Intg";
    


    public IngestPdf(
        IOptions<AiOptions> aiOptions,
        IOptions<IngestionOptions> ingestionOptions,
        IContentTreeProcessor contentTreeProcessor,
        IContentTreeJsonTransformer jsonTransformer,
        IIndexingProcessor indexingProcessor,
        IDocumentClassifier documentClassifier)
    {
        this._indexingProcessor = indexingProcessor;
        this._documentClassifier = documentClassifier;
        this._aiOptionsOptionsContainer = aiOptions;
        this._ingestionOptionsContainer = ingestionOptions;
        this._ingestionOptions = ingestionOptions.Value;
        this._aiOptions = aiOptions.Value;
        this._contentTreeProcessor = contentTreeProcessor;
        this._jsonTransformer = jsonTransformer;
    }

    [Function(nameof(IngestPdf))]
    [BlobOutput("ingest/processed-pdf/{name}", Connection = "ConnectionStrings:IngestionBlobConnectionString")]
    public async Task<Stream> Run(
        [BlobTrigger("ingest/input-pdf/{name}", Connection = "ConnectionStrings:IngestionBlobConnectionString")]
            string pdfItem, string name,
        [BlobInput("ingest/input-pdf/{name}", Connection = "ConnectionStrings:IngestionBlobConnectionString")]
            Stream pdfStream,
        FunctionContext executionContext)
    {
        MemoryStream originalPdfStream = new();
        await pdfStream.CopyToAsync(originalPdfStream);
        originalPdfStream.Position = 0;

        MemoryStream outputStream = new();
        await pdfStream.CopyToAsync(outputStream);
        outputStream.Position = 0;

        MemoryStream indexingStreamForHashing = new();
        await pdfStream.CopyToAsync(indexingStreamForHashing);
        indexingStreamForHashing.Position = 0;

        List<ContentNode> contentTree = new List<ContentNode>();

        var blobServiceClient = new BlobServiceClient(this._ingestionOptions.BlobStorageConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient("ingest");
        var originalBlobClient = containerClient.GetBlobClient($"input-pdf/{name}");

        // If classification is disabled, we don't want to process it.
        if (!this._ingestionOptions.PerformClassification)
        {
            Console.WriteLine("Classification is disabled - assuming we are dealing with an Environmental report with numbered chapters and sections");
            this._pdfPipeline = new NuclearEnvironmentalReportPdfPipeline(this._aiOptionsOptionsContainer, this._contentTreeProcessor, this._jsonTransformer);
            contentTree = await this._pdfPipeline.RunAsync(originalPdfStream, name);
        }
        else
        {
            // Generate a 15-minute SAS token for the blob
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "ingest",
                BlobName = $"input-pdf/{name}",
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow,
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            // Parse account name and account key from full Azure Blob Storage Connection String
            var blobAccountName = this._ingestionOptions.BlobStorageAccountName;
            var blobAccountKey = this._ingestionOptions.BlobStorageAccountKey;

            // Build the SAS token
            var sasToken = sasBuilder
                .ToSasQueryParameters(new StorageSharedKeyCredential(blobAccountName, blobAccountKey)).ToString();

            // Get the SAS URI string for the blob
            var sasUri = $"{originalBlobClient.Uri}?{sasToken}";

            // Classify the document
            var documentClassification = await this._documentClassifier.ClassifyDocumentFromUri(sasUri, this._ingestionOptions.ClassificationModelName);

            // If the document is not classified, we don't want to process it.
            if (!documentClassification.SuccessfulClassification)
            {
                Console.WriteLine("Document failed classification - aborting and moving to processed");
                return outputStream;
            }

            switch (documentClassification.ClassificationType)
            {
                case DocumentClassificationType.EnvironmentalReportWithMixedTitles:
                    Console.WriteLine("Document classified as EnvironmentalReportWithMixedTitles");
                    Console.WriteLine("We can't process this further - moving to processed without further work");
                    // Delete the original blob
                    await originalBlobClient.DeleteIfExistsAsync();
                    return outputStream;
                case DocumentClassificationType.EnvironmentalReportWithNumberedChapters:
                    Console.WriteLine("Document classified as EnvironmentalReportWithNumberedChapters");
                    this._pdfPipeline = new NuclearEnvironmentalReportPdfPipeline(this._aiOptionsOptionsContainer,
                        this._contentTreeProcessor, this._jsonTransformer);
                    contentTree = await this._pdfPipeline.RunAsync(originalPdfStream, name);
                    break;
                case DocumentClassificationType.FiguresAndTablesOnly:
                    Console.WriteLine("Document classified as FiguresAndTablesOnly");
                    Console.WriteLine("We can't process this further - moving to processed without further work");
                    // Delete the original blob
                    await originalBlobClient.DeleteIfExistsAsync();
                    return outputStream;
                case DocumentClassificationType.HeadingsOnly:
                    Console.WriteLine("Document classified as HeadingsOnly");
                    Console.WriteLine("We can't process this further - moving to processed without further work");
                    // Delete the original blob
                    await originalBlobClient.DeleteIfExistsAsync();
                    return outputStream;
                case DocumentClassificationType.WithHeldSectionCover:
                    Console.WriteLine("Document classified as WithHeldSectionCover");
                    Console.WriteLine("We can't process this further - moving to processed without further work");
                    // Delete the original blob
                    await originalBlobClient.DeleteIfExistsAsync();
                    return outputStream;
                default:
                    Console.WriteLine("Document failed classification - aborting and moving to processed");
                    Console.WriteLine("We can't process this further - moving to processed without further work");
                    // Delete the original blob
                    await originalBlobClient.DeleteIfExistsAsync();
                    return outputStream;
            }
        }

        // Generate JSON from Content Tree
        // Store JSON in Cognitive Search while generating embeddings
        if (contentTree.Count > 0)
        {
            await this._indexingProcessor.IndexAndStoreContentNodesAsync(contentTree, name, indexingStreamForHashing);
        }

        // Delete the original blob
        await originalBlobClient.DeleteIfExistsAsync();

        // This uses the BlobOutput defined on the Function and writes the output to the blob storage.
        // This is why we only delete the original blob instead of "moving it".
        return outputStream;
    }

    // Additional Functions:
    // On a schedule, move PDFs that haven't been picked up to a "retry" folder (the blob trigger eventually times out if too many failed attempts or downtime)

}
