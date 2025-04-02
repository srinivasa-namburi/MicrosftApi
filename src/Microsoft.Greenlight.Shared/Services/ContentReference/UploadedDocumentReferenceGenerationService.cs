using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.Search;
using System.IO;
using System.Text;

namespace Microsoft.Greenlight.Shared.Services.ContentReference;

/// <summary>
/// Service for generating content references from uploaded document files
/// </summary>
public class UploadedDocumentReferenceGenerationService : IContentReferenceGenerationService<ExportedDocumentLink>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<UploadedDocumentReferenceGenerationService> _logger;
    private readonly IKernelMemoryInstanceFactory _kernelMemoryFactory;
    private readonly IAiEmbeddingService _aiEmbeddingService;
    private readonly Helpers.AzureFileHelper _fileHelper;

    /// <summary>
    /// Initializes a new instance of the uploaded document reference generation service.
    /// </summary>
    public UploadedDocumentReferenceGenerationService(
        DocGenerationDbContext dbContext,
        ILogger<UploadedDocumentReferenceGenerationService> logger,
        IKernelMemoryInstanceFactory kernelMemoryFactory,
        IAiEmbeddingService aiEmbeddingService,
        Helpers.AzureFileHelper fileHelper)
    {
        _dbContext = dbContext;
        _logger = logger;
        _kernelMemoryFactory = kernelMemoryFactory;
        _aiEmbeddingService = aiEmbeddingService;
        _fileHelper = fileHelper;
    }

    /// <inheritdoc />
    public async Task<List<ContentReferenceItemInfo>> GenerateReferencesAsync(ExportedDocumentLink uploadedFile)
    {
        // Only generate references for temporary reference files
        if (uploadedFile.Type != FileDocumentType.TemporaryReferenceFile)
        {
            return new List<ContentReferenceItemInfo>();
        }

        return new List<ContentReferenceItemInfo>
        {
            new ContentReferenceItemInfo
            {
                Id = Guid.NewGuid(),
                ContentReferenceSourceId = uploadedFile.Id,
                DisplayName = uploadedFile.FileName,
                ReferenceType = ContentReferenceType.ExternalFile,
                CreatedDate = uploadedFile.Created.DateTime,
                Description = $"Uploaded document: {uploadedFile.FileName}",
                CreatedUtc = uploadedFile.Created.UtcDateTime
            }
        };
    }

    /// <inheritdoc />
    public async Task<string?> GenerateContentTextForRagAsync(Guid uploadedFileId)
    {
        try
        {
            var uploadedFile = await _dbContext.ExportedDocumentLinks
                .Where(x => x.Type == FileDocumentType.TemporaryReferenceFile)
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == uploadedFileId);

            if (uploadedFile == null)
            {
                _logger.LogWarning("Uploaded file with ID {UploadedFileId} not found", uploadedFileId);
                return null;
            }


            // Get the file as a stream
            var fileStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(uploadedFile.AbsoluteUrl);
            if (fileStream == null)
            {
                _logger.LogWarning("Unable to retrieve file stream for {FileId}", uploadedFileId);
                return null;
            }

            // Use KernelMemory to extract text
            var memory = _kernelMemoryFactory.GetKernelMemoryForAdhocUploads();

            // We'll use a unique ID for this import based on the file ID
            string documentId = $"temp-extraction-{uploadedFileId}";


            // Import the document (just for text extraction, we'll remove it later)
            await using (fileStream)
            {
                await memory.ImportDocumentAsync(
                    documentId: documentId,
                    content: fileStream,
                    fileName: uploadedFile.FileName,
                    steps: new List<string>() { "extract" }
                    );
            }

            // Various ways to retrieve the document content
            var streamableFileContent = await memory.ExportFileAsync(documentId, $"{uploadedFile.FileName}.extract.txt");
            var fileStreamData = await streamableFileContent.GetStreamAsync();

            // Read the filestream data into a string
            string fileContent = "";
            using (var reader = new StreamReader(fileStreamData))
            {
                fileContent = reader.ReadToEnd();
            }

            // Clean up the temporary document
            await memory.DeleteDocumentAsync(documentId);

            // Build the text content from the search results
            if (fileContent.Length > 0)
            {
                StringBuilder contentBuilder = new StringBuilder();
                contentBuilder.AppendLine($"ExternalFile: {uploadedFile.FileName}");
                contentBuilder.AppendLine("------------------------------------");
                contentBuilder.AppendLine(fileContent);
                contentBuilder.AppendLine("------------------------------------");
                return contentBuilder.ToString();
            }

            // Fallback if we can't extract text or for non-document assets
            return $"ExternalFile: {uploadedFile.FileName}\n" +
                   $"Type: {uploadedFile.MimeType}\n" +
                   $"Created: {uploadedFile.Created}\n" +
                   $"Document ID: {uploadedFile.Id}\n" +
                   $"Error : Unable to process this file";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating content text for RAG for uploaded file ID {UploadedFileId}", uploadedFileId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingsAsync(Guid uploadedFileId)
    {
        try
        {
            var contentText = await GenerateContentTextForRagAsync(uploadedFileId);
            if (string.IsNullOrEmpty(contentText))
            {
                _logger.LogWarning("No content text available for uploaded file {UploadedFileId}", uploadedFileId);
                return Array.Empty<float>();
            }

            // Use the AI embedding service to generate embeddings
            return await _aiEmbeddingService.GenerateEmbeddingsAsync(contentText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings for uploaded file {UploadedFileId}", uploadedFileId);
            return Array.Empty<float>();
        }
    }
}
