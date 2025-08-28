// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel;
using System.Security.Claims;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using ModelContextProtocol.Server;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;

namespace Microsoft.Greenlight.McpServer.Tools;

/// <summary>
/// MCP tools exposing core Greenlight operations like listing processes/libraries,
/// starting generation, ingestion, and querying metadata fields for payload construction.
/// </summary>
[McpServerToolType]
public static class GreenlightTools
{
    private static string? TryGetUserOid(IHttpContextAccessor http)
    {
        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Common claim types for Azure AD/JWTs
        // oid (Azure AD), http://schemas.microsoft.com/identity/claims/objectidentifier
        // nameidentifier, sub as fallback
        var oid = user.FindFirst("oid")?.Value
                  ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? user.FindFirst("sub")?.Value;

        // Normalize to Guid if possible
        if (Guid.TryParse(oid, out var guid))
        {
            return guid.ToString();
        }

        return oid; // fall back to raw value
    }

    /// <summary>
    /// Lists available document processes (both dynamic and static combined).
    /// </summary>
    [McpServerTool(Name = "list_document_processes"), Description("Lists available document processes")]
    public static async Task<List<DocumentProcessInfo>> ListDocumentProcessesAsync(
        IDocumentProcessInfoService documentProcessInfoService,
        CancellationToken cancellationToken)
    {
        var list = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        return list;
    }

    /// <summary>
    /// Lists available document libraries.
    /// </summary>
    [McpServerTool(Name = "list_document_libraries"), Description("Lists available document libraries")]
    public static async Task<List<DocumentLibraryInfo>> ListDocumentLibrariesAsync(
        IDocumentLibraryInfoService documentLibraryInfoService,
        CancellationToken cancellationToken)
    {
        var list = await documentLibraryInfoService.GetAllDocumentLibrariesAsync();
        return list;
    }

    /// <summary>
    /// Arguments for starting a document generation orchestration.
    /// </summary>
    public sealed record StartDocumentGenerationArgs
    {
        /// <summary>
        /// Process short name identifying the target document process.
        /// </summary>
        [Description("Process short name")]
        public required string documentProcessName { get; init; }

        /// <summary>
        /// Title for the generated document.
        /// </summary>
        [Description("Title for the document")]
        public required string documentTitle { get; init; }

        /// <summary>
        /// Optional author object ID (GUID as string) to attribute authorship.
        /// </summary>
        [Description("Optional author OID (GUID string)")]
        public string? authorOid { get; init; }

        /// <summary>
        /// Optional metadata model name to drive request field validation and defaults.
        /// </summary>
        [Description("Optional metadata model name")]
        public string? metadataModelName { get; init; }

        /// <summary>
        /// Optional JSON payload for generation request. Keys should match metadata field names.
        /// </summary>
        [Description("Optional JSON payload for generation request")]
        public string? requestAsJson { get; init; }
    }

    /// <summary>
    /// Starts a document generation orchestration for the specified process and title.
    /// Returns a JSON with status (started/failed) and the generated document ID.
    /// Use the returned ID with get_document_generation_status or get_document_generation_full_status.
    /// </summary>
    [McpServerTool(Name = "start_document_generation"), Description("Starts a document generation orchestration and returns JSON { status, id }. Use the returned id with get_document_generation_status or get_document_generation_full_status.")]
    public static async Task<string> StartDocumentGenerationAsync(
        IHttpContextAccessor httpContextAccessor,
        Orleans.IClusterClient clusterClient,
        StartDocumentGenerationArgs args,
        CancellationToken cancellationToken)
    {
        var author = args.authorOid ?? TryGetUserOid(httpContextAccessor);

        var dto = new GenerateDocumentDTO
        {
            DocumentProcessName = args.documentProcessName,
            DocumentTitle = args.documentTitle,
            AuthorOid = author,
            RequestAsJson = args.requestAsJson,
            Id = Guid.NewGuid()
        };

        try
        {
            var grain = clusterClient.GetGrain<IDocumentGenerationOrchestrationGrain>(dto.Id);
            await grain.StartDocumentGenerationAsync(dto);
            var payload = System.Text.Json.JsonSerializer.Serialize(new { status = "started", id = dto.Id });
            return payload;
        }
        catch (Exception ex)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new { status = "failed", error = ex.Message, id = dto.Id });
            return payload;
        }
    }

    /// <summary>
    /// Arguments for getting document generation status.
    /// </summary>
    public sealed record GetDocumentStatusArgs
    {
        /// <summary>
        /// Generated document ID (GUID as string). Use the ID returned by start_document_generation.
        /// </summary>
        [Description("Generated document ID (GUID string) returned by start_document_generation")]
        public required string documentId { get; init; }
    }

    /// <summary>
    /// Gets the overall/summary document generation status for a document.
    /// Use the ID returned by start_document_generation.
    /// </summary>
    [McpServerTool(Name = "get_document_generation_status"), Description("Gets overall document generation status for a document. Use the id returned by start_document_generation.")]
    public static async Task<DocumentGenerationStatusInfo?> GetDocumentGenerationStatusAsync(
        IDocumentGenerationApiClient documentGenerationApiClient,
        GetDocumentStatusArgs args,
        CancellationToken cancellationToken)
    {
        return await documentGenerationApiClient.GetDocumentGenerationStatusAsync(args.documentId);
    }

    /// <summary>
    /// Gets the full, per-node document generation status details for a document (including node hierarchy and computed statuses).
    /// Use the ID returned by start_document_generation.
    /// </summary>
    [McpServerTool(Name = "get_document_generation_full_status"), Description("Gets full per-node document generation status for a document. Use the id returned by start_document_generation.")]
    public static async Task<DocumentGenerationFullStatusInfo?> GetDocumentGenerationFullStatusAsync(
        IDocumentGenerationApiClient documentGenerationApiClient,
        GetDocumentStatusArgs args,
        CancellationToken cancellationToken)
    {
        return await documentGenerationApiClient.GetDocumentGenerationFullStatusAsync(args.documentId);
    }

    /// <summary>
    /// Lists generated documents available to the current user or context.
    /// </summary>
    [McpServerTool(Name = "list_generated_documents"), Description("Lists generated documents; use items' ids with get_document_generation_status or get_document_generation_full_status.")]
    public static async Task<List<GeneratedDocumentListItem>> ListGeneratedDocumentsAsync(
        IDocumentGenerationApiClient documentGenerationApiClient,
        CancellationToken cancellationToken)
    {
        return await documentGenerationApiClient.GetGeneratedDocumentsAsync();
    }

    /// <summary>
    /// Arguments for registering and ingesting a document by URL.
    /// </summary>
    public sealed record IngestDocumentArgs
    {
        /// <summary>
        /// Short name of the target document library or process.
        /// </summary>
        [Description("Short name of the document library or process")]
        public required string targetShortName { get; init; }

        /// <summary>
        /// Target type: "primary_process" or "additional_library".
        /// </summary>
        [Description("Type: primary_process or additional_library")]
        public required string targetType { get; init; }

        /// <summary>
        /// Original document URL (blob or http) from which content can be fetched.
        /// </summary>
        [Description("Original document URL (blob or http)")]
        public required string documentUrl { get; init; }

        /// <summary>
        /// File name including extension to associate with the ingested document.
        /// </summary>
        [Description("File name including extension")]
        public required string fileName { get; init; }

        /// <summary>
        /// Optional uploader user OID.
        /// </summary>
        [Description("Optional uploader user OID")]
        public string? uploaderOid { get; init; }
    }

    /// <summary>
    /// Registers a document by URL and ingests it into the appropriate vector store based on target type.
    /// Returns the new ingested document ID (Guid as string).
    /// </summary>
    [McpServerTool(Name = "upload_and_ingest_document"), Description("Registers a document by URL and ingests into vector store")]
    public static async Task<string> UploadAndIngestDocumentAsync(
        IHttpContextAccessor httpContextAccessor,
        IDocumentIngestionService documentIngestionService,
        IngestDocumentArgs args,
        CancellationToken cancellationToken)
    {
        var libraryType = args.targetType.Equals("additional_library", StringComparison.OrdinalIgnoreCase)
            ? DocumentLibraryType.AdditionalDocumentLibrary
            : DocumentLibraryType.PrimaryDocumentProcessLibrary;

        // For ingestion service we need an ID up-front
        var ingestedId = Guid.NewGuid();

        var uploader = args.uploaderOid ?? TryGetUserOid(httpContextAccessor);

        using var dummyStream = Stream.Null; // ingestion implementations may fetch by URL if supported

        var result = await documentIngestionService.IngestDocumentAsync(
            ingestedId,
            dummyStream,
            args.fileName,
            args.documentUrl,
            args.targetShortName,
            args.targetShortName,
            uploader,
            null);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Ingestion failed");
        }

        return ingestedId.ToString();
    }

    /// <summary>
    /// Arguments for retrieving document process metadata fields to construct valid JSON payloads.
    /// Provide either processId (Guid string) or processShortName.
    /// </summary>
    public sealed record GetMetadataFieldsArgs
    {
        /// <summary>
        /// Document process ID (GUID as string).
        /// </summary>
        [Description("Document process ID (GUID as string)")]
        public string? processId { get; init; }

        /// <summary>
        /// Document process short name.
        /// </summary>
        [Description("Document process short name")]
        public string? processShortName { get; init; }
    }

    /// <summary>
    /// Gets the metadata fields configured for a dynamic document process. These define valid keys and types
    /// for the RequestAsJson payload when starting generation.
    /// </summary>
    [McpServerTool(Name = "get_document_process_metadata_fields"), Description("Gets metadata field definitions for a document process")]
    public static async Task<List<DocumentProcessMetadataFieldInfo>> GetDocumentProcessMetadataFieldsAsync(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IDocumentProcessInfoService documentProcessInfoService,
        IMapper mapper,
        GetMetadataFieldsArgs args,
        CancellationToken cancellationToken)
    {
        // Resolve processId
        Guid processId = Guid.Empty;

        if (!string.IsNullOrWhiteSpace(args.processId) && Guid.TryParse(args.processId, out var parsed))
        {
            processId = parsed;
        }
        else if (!string.IsNullOrWhiteSpace(args.processShortName))
        {
            var dpi = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(args.processShortName);
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
