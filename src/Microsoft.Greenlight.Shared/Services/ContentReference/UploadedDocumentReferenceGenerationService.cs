// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using System.Text;

namespace Microsoft.Greenlight.Shared.Services.ContentReference;

/// <summary>
/// Legacy: generates ExternalFile content references from <see cref="ExportedDocumentLink"/>.
/// New uploads should prefer the FileStorageSource + ExternalLinkAsset path
/// (see FileController.UploadTemporaryReferenceFile and IFileStorageService).
/// </summary>
public class UploadedDocumentReferenceGenerationService : IContentReferenceGenerationService<ExportedDocumentLink>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<UploadedDocumentReferenceGenerationService> _logger;
    private readonly IAiEmbeddingService _aiEmbeddingService;
    private readonly Helpers.AzureFileHelper _fileHelper;
    private readonly ITextExtractionService _textExtractionService;

    /// <summary>
    /// Initializes a new instance of the uploaded document reference generation service.
    /// </summary>
    public UploadedDocumentReferenceGenerationService(
        DocGenerationDbContext dbContext,
        ILogger<UploadedDocumentReferenceGenerationService> logger,
        IAiEmbeddingService aiEmbeddingService,
        Helpers.AzureFileHelper fileHelper,
        ITextExtractionService textExtractionService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _aiEmbeddingService = aiEmbeddingService;
        _fileHelper = fileHelper;
        _textExtractionService = textExtractionService;
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
                .Where(x => x.Type == FileDocumentType.TemporaryReferenceFile || x.Type == FileDocumentType.Review)
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

            // Extract text directly using the shared text extraction service (Semantic Kernel pipeline)
            string fileContent = string.Empty;
            await using (fileStream)
            {
                fileContent = await _textExtractionService.ExtractTextAsync(fileStream, uploadedFile.FileName);
            }

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
