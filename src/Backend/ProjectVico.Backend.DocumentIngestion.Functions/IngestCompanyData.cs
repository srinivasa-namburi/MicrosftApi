// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Classification;
using ProjectVico.Backend.DocumentIngestion.Shared.Classification.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Pipelines;

namespace ProjectVico.Backend.DocumentIngestion.API.Functions;

public class IngestCompanyData
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



    public IngestCompanyData(
        IOptions<AiOptions> aiOptions,
        IOptions<IngestionOptions> ingestionOptions,
        IContentTreeProcessor contentTreeProcessor,
        IContentTreeJsonTransformer jsonTransformer,
        IIndexingProcessor indexingProcessor,
        [FromKeyedServices("customdata-classifier")]
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

    [Function(nameof(IngestCompanyData))]
    [BlobOutput("ingest-custom/processed-pdf/{name}", Connection = "ConnectionStrings:IngestionBlobConnectionString")]
    public async Task<Stream> Run(
        [BlobTrigger("ingest-custom/input-pdf/{name}", Connection = "ConnectionStrings:IngestionBlobConnectionString")]
            string pdfItem, string name,
        [BlobInput("ingest-custom/input-pdf/{name}", Connection = "ConnectionStrings:IngestionBlobConnectionString")]
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
        var containerClient = blobServiceClient.GetBlobContainerClient("ingest-custom");
        var originalBlobClient = containerClient.GetBlobClient($"input-pdf/{name}");

        // If classification is disabled, we don't want to process it.
        if (!this._ingestionOptions.PerformCustomDataClassification)
        {
            Console.WriteLine("Classification is disabled - baseline processing grab");
            this._pdfPipeline = new BaselinePipeline(this._aiOptionsOptionsContainer, this._contentTreeProcessor, this._jsonTransformer);
            contentTree = await this._pdfPipeline.RunAsync(originalPdfStream, name);
        }
        else
        {
            // Generate a 15-minute SAS token for the blob
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "ingest-custom",
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
            var documentClassification = await this._documentClassifier.ClassifyDocumentFromUri(sasUri, this._ingestionOptions.CustomDataClassificationModelName);

            // If the document is not classified, we don't want to process it.
            if (!documentClassification.SuccessfulClassification)
            {
                // cannot identify the document as any of these pre - defined types with their own processing pipeline
                Console.WriteLine("Document failed classification - baseline processing grab");
                this._pdfPipeline = new BaselinePipeline(this._aiOptionsOptionsContainer,
                    this._contentTreeProcessor, this._jsonTransformer);
                contentTree = await this._pdfPipeline.RunAsync(originalPdfStream, name);
            }
            else
            {
                switch (documentClassification.ClassificationType)
                {

                    case DocumentClassificationType.CustomDataProductSpecificInput:
                        // file with a pre-set structure with details of this new project (e.g. The Reactor Type, Temperature, GPS Coords of the project, etc.)
                        Console.WriteLine("Document classified as ProductSpecificInput");
                        this._pdfPipeline = new ProductSpecificInputPipeline(this._aiOptionsOptionsContainer,
                            this._contentTreeProcessor, this._jsonTransformer);
                        contentTree = await this._pdfPipeline.RunAsync(originalPdfStream, name);
                        break;

                    default:
                    case DocumentClassificationType.CustomDataBasicDocument:
                        // Document is a Numbered Structured Document
                        Console.WriteLine("Document classified as NumberedStructuredDocument");
                        this._pdfPipeline = new BaselinePipeline(this._aiOptionsOptionsContainer,
                            this._contentTreeProcessor, this._jsonTransformer);
                        contentTree = await this._pdfPipeline.RunAsync(originalPdfStream, name);
                        break;
                }
            }
        }

        // Generate JSON from Content Tree
        // Store JSON in Cognitive Search while generating embeddings
        if (contentTree.Count > 0)
        {
            await this._indexingProcessor.IndexAndStoreCustomNodesAsync(contentTree, name, indexingStreamForHashing);
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
