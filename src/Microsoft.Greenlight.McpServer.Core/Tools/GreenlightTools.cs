// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.McpServer.Core.Contracts.Requests;
using Microsoft.Greenlight.McpServer.Core.Contracts.Responses;
using Microsoft.Greenlight.McpServer.Core.Services;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using ModelContextProtocol.Server;

namespace Microsoft.Greenlight.McpServer.Core.Tools;

/// <summary>
/// MCP tools exposing core Greenlight operations like listing processes/libraries,
/// starting generation, ingestion, and querying metadata fields for payload construction.
/// </summary>
[McpServerToolType]
public static class GreenlightTools
{
    /// <summary>
    /// Lists available document processes (both dynamic and static combined).
    /// </summary>
    [McpServerTool(Name = "list_document_processes"), 
     Description("Lists available document processes (document types) that can be generated")]
    public static async Task<List<DocumentProcessInfo>> ListDocumentProcessesAsync(
        IDocumentProcessInfoService documentProcessInfoService,
        CancellationToken cancellationToken)
    {
        var list = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        return list;
    }

    /// <summary>
    /// Starts a document generation orchestration for the specified process and title.
    /// Returns a structured response with status and the generation id.
    /// </summary>
    [McpServerTool(Name = "start_document_generation"),
     Description("Starts a document generation orchestration and returns structured response with status and id." +
                 "Use the get_document_process_metadata_fields tool to get metadata fields for the document type. " +
                 "The fields returned by get_document_process_metadata_fields go in the metadataFields key, and should" +
                 "be formatted as a json array with \"fieldname\":\"value\" for each metadata field we send ")]
    public static async Task<StartDocumentGenerationResponse> StartDocumentGenerationAsync(
        McpRequestContext requestContext,
        IClusterClient clusterClient,
        StartDocumentGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var author = requestContext.ProviderSubjectId;

        var dto = new GenerateDocumentDTO
        {
            DocumentProcessName = request.documentProcessName,
            DocumentTitle = request.documentTitle,
            AuthorOid = author,
            RequestAsJson = request.metadataFields,
            Id = Guid.NewGuid()
        };

        try
        {
            var grain = clusterClient.GetGrain<IDocumentGenerationOrchestrationGrain>(dto.Id);
            await grain.StartDocumentGenerationAsync(dto);
            return new StartDocumentGenerationResponse
            {
                Status = "started",
                Id = dto.Id
            };
        }
        catch (Exception ex)
        {
            return new StartDocumentGenerationResponse
            {
                Status = "failed",
                Id = dto.Id,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets the overall/summary document generation status for a document.
    /// Use the ID returned by start_document_generation.
    /// </summary>
    [McpServerTool(Name = "get_document_generation_status"), 
     Description("Gets overall document generation status for a document. " +
                 "Use a documentId returned by start_document_generation or list_generated_documents.")]
    public static async Task<DocumentGenerationStatusInfo?> GetDocumentGenerationStatusAsync(
        IDocumentGenerationApiClient documentGenerationApiClient,
        GetDocumentStatusRequest request,
        CancellationToken cancellationToken)
    {
        return await documentGenerationApiClient.GetDocumentGenerationStatusAsync(request.documentId);
    }

    /// <summary>
    /// Gets the full, per-node document generation status details for a document (including node hierarchy and computed statuses).
    /// Use the ID returned by start_document_generation.
    /// </summary>
    [McpServerTool(Name = "get_document_generation_full_status"), 
     Description("Gets full per-section document generation status for a document. This allows you to also get " +
                 "the structure and document outline/section overview for a document. Use the id returned by start_document_generation.")]
    public static async Task<DocumentGenerationFullStatusInfo?> GetDocumentGenerationFullStatusAsync(
        IDocumentGenerationApiClient documentGenerationApiClient,
        GetDocumentStatusRequest request,
        CancellationToken cancellationToken)
    {
        return await documentGenerationApiClient.GetDocumentGenerationFullStatusAsync(request.documentId);
    }

    /// <summary>
    /// Lists generated documents available to the current user or context.
    /// </summary>
    [McpServerTool(Name = "list_generated_documents"), 
     Description("Lists generated documents - returns the ID and name of those documents.")]
    public static async Task<List<GeneratedDocumentListItem>> ListGeneratedDocumentsAsync(
        IDocumentGenerationApiClient documentGenerationApiClient,
        CancellationToken cancellationToken)
    {
        return await documentGenerationApiClient.GetGeneratedDocumentsAsync();
    }

    /// <summary>
    /// Registers a document by URL and ingests it into the appropriate vector store based on target type.
    /// Returns a structured response with the ingested document id.
    /// </summary>
    [McpServerTool(Name = "upload_and_ingest_document"),
     Description("Registers a document by URL and ingests into vector store")]
    public static async Task<UploadAndIngestDocumentResponse> UploadAndIngestDocumentAsync(
        McpRequestContext requestContext,
        IDocumentIngestionService documentIngestionService,
        IngestDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var libraryType = request.targetType.Equals("additional_library", StringComparison.OrdinalIgnoreCase)
            ? DocumentLibraryType.AdditionalDocumentLibrary
            : DocumentLibraryType.PrimaryDocumentProcessLibrary;

        // For ingestion service we need an ID up-front
        var ingestedId = Guid.NewGuid();

        var uploader = request.uploaderOid ?? requestContext.ProviderSubjectId;

        using var dummyStream = Stream.Null; // ingestion implementations may fetch by URL if supported

        var result = await documentIngestionService.IngestDocumentAsync(
            ingestedId,
            dummyStream,
            request.fileName,
            request.documentUrl,
            request.targetShortName,
            request.targetShortName,
            uploader,
            null);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Ingestion failed");
        }

        return new UploadAndIngestDocumentResponse
        {
            IngestedDocumentId = ingestedId
        };
    }

    /// <summary>
    /// Gets the metadata fields configured for a dynamic document process. These define valid keys and types
    /// for the RequestAsJson payload when starting generation.
    /// </summary>
    [McpServerTool(Name = "get_document_process_metadata_fields"), 
     Description("Gets metadata field definitions for a document process")]
    public static async Task<List<DocumentProcessMetadataFieldInfo>> GetDocumentProcessMetadataFieldsAsync(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IDocumentProcessInfoService documentProcessInfoService,
        IMapper mapper,
        GetMetadataFieldsRequest request,
        CancellationToken cancellationToken)
    {
        // Resolve processId
        Guid processId = Guid.Empty;

        if (!string.IsNullOrWhiteSpace(request.processId) && Guid.TryParse(request.processId, out var parsed))
        {
            processId = parsed;
        }
        else if (!string.IsNullOrWhiteSpace(request.processShortName))
        {
            var dpi = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(request.processShortName);
            processId = dpi?.Id ?? Guid.Empty;
        }

        if (processId == Guid.Empty)
        {
            // Static processes or unresolved name don't have dynamic metadata fields
            return new List<DocumentProcessMetadataFieldInfo>();
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var metadataFields = await db.DynamicDocumentProcessMetaDataFields
            .Where(x => x.DynamicDocumentProcessDefinitionId == processId)
            .ToListAsync(cancellationToken);

        if (metadataFields.Count == 0)
        {
            return new List<DocumentProcessMetadataFieldInfo>();
        }

        var result = mapper.Map<List<DocumentProcessMetadataFieldInfo>>(metadataFields);
        // Ensure deterministic order for UI/payloads
        result = result.OrderBy(x => x.Order).ThenBy(x => x.Name).ToList();
        return result;
    }
}
