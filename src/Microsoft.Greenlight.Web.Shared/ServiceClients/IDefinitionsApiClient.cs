// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO.Definitions;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients
{
    /// <summary>
    /// API client for import/export of document processes and document libraries.
    /// </summary>
    public interface IDefinitionsApiClient : IServiceClient
    {
        // Process export/import
        Task<DocumentProcessDefinitionPackageDto?> ExportProcessAsync(Guid processId);
        Task<Guid> ImportProcessAsync(DocumentProcessDefinitionPackageDto package);
        Task<bool> IsProcessShortNameAvailableAsync(string shortName);

        // Library export/import
        Task<DocumentLibraryDefinitionPackageDto?> ExportLibraryAsync(Guid libraryId);
        Task<Guid> ImportLibraryAsync(DocumentLibraryDefinitionPackageDto package);
        Task<bool> IsLibraryShortNameAvailableAsync(string shortName);

        // Index compatibility check
        Task<IndexCompatibilityInfoDto?> GetIndexCompatibilityAsync(string indexName);
    }

    /// <summary>
    /// Index compatibility information DTO for frontend use.
    /// </summary>
    public class IndexCompatibilityInfoDto
    {
        public string IndexName { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public bool IsSkLayout { get; set; }
        public int? MatchedEmbeddingDimensions { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string? Error { get; set; }
    }
}
