using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.ServiceClients
{
    public class DocumentLibraryApiClient : BaseServiceClient<DocumentLibraryApiClient>, IDocumentLibraryApiClient
    {
        public DocumentLibraryApiClient(HttpClient httpClient, ILogger<DocumentLibraryApiClient> logger, AuthenticationStateProvider authStateProvider)
            : base(httpClient, logger, authStateProvider)
        {
        }

        public async Task<List<DocumentLibraryInfo>> GetAllDocumentLibrariesAsync()
        {
            var response = await SendGetRequestMessage("/api/document-libraries");
            response.EnsureSuccessStatusCode();
            var libraries = await response.Content.ReadFromJsonAsync<List<DocumentLibraryInfo>>();
            return libraries ?? new List<DocumentLibraryInfo>();
        }

        public async Task<DocumentLibraryInfo?> GetDocumentLibraryByIdAsync(Guid id)
        {
            var response = await SendGetRequestMessage($"/api/document-libraries/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DocumentLibraryInfo>();
        }

        public async Task<List<DocumentProcessInfo>> GetDocumentProcessesByLibraryIdAsync(Guid libraryId)
        {
            var response = await SendGetRequestMessage($"/api/document-processes/by-document-library/{libraryId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new List<DocumentProcessInfo>();
            }
            response.EnsureSuccessStatusCode();
            var documentProcesses = await response.Content.ReadFromJsonAsync<List<DocumentProcessInfo>>();
            return documentProcesses ?? new List<DocumentProcessInfo>();
        }

        public async Task<List<DocumentLibraryInfo>> GetDocumentLibrariesByProcessIdAsync(Guid processId)
        {
            var response = await SendGetRequestMessage($"/api/document-libraries/by-document-process/{processId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new List<DocumentLibraryInfo>();
            }
            response.EnsureSuccessStatusCode();
            var documentLibraries = await response.Content.ReadFromJsonAsync<List<DocumentLibraryInfo>>();
            return documentLibraries ?? new List<DocumentLibraryInfo>();
        }

        public async Task<DocumentLibraryInfo?> GetDocumentLibraryByShortNameAsync(string shortName)
        {
            var response = await SendGetRequestMessage($"/api/document-libraries/shortname/{Uri.EscapeDataString(shortName)}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DocumentLibraryInfo>();
        }

        public async Task<DocumentLibraryInfo> CreateDocumentLibraryAsync(DocumentLibraryInfo documentLibraryInfo)
        {
            var response = await SendPostRequestMessage("/api/document-libraries", documentLibraryInfo);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DocumentLibraryInfo>();
        }

        public async Task<DocumentLibraryInfo> UpdateDocumentLibraryAsync(DocumentLibraryInfo documentLibraryInfo)
        {
            var response = await SendPutRequestMessage($"/api/document-libraries/{documentLibraryInfo.Id}", documentLibraryInfo);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DocumentLibraryInfo>();
        }

        public async Task<bool> DeleteDocumentLibraryAsync(Guid id)
        {
            var response = await SendDeleteRequestMessage($"/api/document-libraries/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            response.EnsureSuccessStatusCode();
            return true;
        }

        public async Task AssociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId)
        {
            var response = await SendPostRequestMessage($"/api/document-libraries/{documentLibraryId}/document-processes/{documentProcessId}/associate", null, true);
            response.EnsureSuccessStatusCode();
        }

        public async Task DisassociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId)
        {
            var response = await SendPostRequestMessage($"/api/document-libraries/{documentLibraryId}/document-processes/{documentProcessId}/disassociate", null, true);
            response.EnsureSuccessStatusCode();
        }
    }
}
