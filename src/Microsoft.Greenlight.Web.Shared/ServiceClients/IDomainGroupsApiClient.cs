using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients
{
    public interface IDomainGroupsApiClient : IServiceClient
    {
        Task<DomainGroupInfo?> GetDomainGroupAsync(Guid domainGroupId);
        Task<List<DomainGroupInfo>> GetDomainGroupsAsync();
        Task<DomainGroupInfo> CreateDomainGroupAsync(DomainGroupInfo domainGroup);
        Task<DomainGroupInfo> UpdateDomainGroupAsync(DomainGroupInfo domainGroup);
        Task<bool> DeleteDomainGroupAsync(Guid domainGroupId);
        Task<DomainGroupInfo?> AssociateDocumentProcessAsync(Guid domainGroupId, Guid documentProcessId);
        Task<DomainGroupInfo?> DisassociateDocumentProcessAsync(Guid domainGroupId, Guid documentProcessId);
        
    }
}
