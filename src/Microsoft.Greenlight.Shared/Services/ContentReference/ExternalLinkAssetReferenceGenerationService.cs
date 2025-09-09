// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.FileStorage;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using System.Text;

namespace Microsoft.Greenlight.Shared.Services.ContentReference;

/// <summary>
/// Generates content reference details and RAG text for <see cref="ExternalLinkAsset"/> based references.
/// </summary>
public class ExternalLinkAssetReferenceGenerationService : IContentReferenceGenerationService<ExternalLinkAsset>
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IFileStorageServiceFactory _fileStorageServiceFactory;
    private readonly ITextExtractionService _textExtractionService;
    private readonly ILogger<ExternalLinkAssetReferenceGenerationService> _logger;

    /// <summary>
    /// Creates a new instance of the service.
    /// </summary>
    public ExternalLinkAssetReferenceGenerationService(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IFileStorageServiceFactory fileStorageServiceFactory,
        ITextExtractionService textExtractionService,
        ILogger<ExternalLinkAssetReferenceGenerationService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _fileStorageServiceFactory = fileStorageServiceFactory;
        _textExtractionService = textExtractionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ContentReferenceItemInfo>> GenerateReferencesAsync(ExternalLinkAsset source)
    {
        // Typically created at upload time by FileController; provide idempotent generation when needed.
        var info = new ContentReferenceItemInfo
        {
            Id = Guid.NewGuid(),
            ContentReferenceSourceId = source.Id,
            ReferenceType = ContentReferenceType.ExternalLinkAsset,
            DisplayName = string.IsNullOrWhiteSpace(source.FileName) ? source.Url : System.IO.Path.GetFileName(source.FileName),
            Description = string.IsNullOrWhiteSpace(source.Description) ? $"Uploaded document: {source.FileName}" : source.Description,
            CreatedDate = DateTime.UtcNow
        };

        return new List<ContentReferenceItemInfo> { info };
    }

    /// <inheritdoc />
    public async Task<string?> GenerateContentTextForRagAsync(Guid externalLinkAssetId)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var asset = await db.ExternalLinkAssets
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == externalLinkAssetId);

            if (asset == null)
            {
                _logger.LogWarning("ExternalLinkAsset {AssetId} not found", externalLinkAssetId);
                return null;
            }

            // Try to get a storage-backed stream using the configured FileStorageSource
            Stream? fileStream = null;
            string logicalFileName = string.IsNullOrWhiteSpace(asset.FileName) ? "external-asset" : System.IO.Path.GetFileName(asset.FileName);

            try
            {
                if (asset.FileStorageSourceId.HasValue)
                {
                    var service = await _fileStorageServiceFactory.GetServiceBySourceIdAsync(asset.FileStorageSourceId.Value);
                    if (service != null && !string.IsNullOrWhiteSpace(asset.FileName))
                    {
                        fileStream = await service.GetFileStreamAsync(asset.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to open stream from storage source for asset {AssetId}", asset.Id);
            }

            if (fileStream == null)
            {
                _logger.LogWarning("Unable to retrieve file stream for ExternalLinkAsset {AssetId}", asset.Id);
                return $"External Asset: {logicalFileName}\nType: {asset.MimeType}\nCreated: {asset.CreatedUtc}\nAsset ID: {asset.Id}\nError: Unable to access file content";
            }

            await using (fileStream)
            {
                var text = await _textExtractionService.ExtractTextAsync(fileStream, logicalFileName);
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("No text extracted from ExternalLinkAsset {AssetId}", asset.Id);
                    return $"External Asset: {logicalFileName}\nType: {asset.MimeType}\nCreated: {asset.CreatedUtc}\nAsset ID: {asset.Id}\nError: Unable to extract text";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"--- EXTERNAL ASSET: {logicalFileName} ---");
                sb.AppendLine();
                sb.AppendLine(text);
                return sb.ToString().Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating content text for RAG for ExternalLinkAsset {AssetId}", externalLinkAssetId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingsAsync(Guid externalLinkAssetId)
    {
        // Not used directly in current pipeline; embeddings are handled by the vector repository
        return Array.Empty<float>();
    }
}

