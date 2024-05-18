using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Dynamic.Services;

public interface IDocumentDefinitionService
{
    IQueryable<IDocumentProcessDefinition> GetCombinedListOfDocumentDefinitionsAsync();
    Task<IDocumentProcessDefinition?> GetDocumentDefinitionByShortNameAsync(string shortName);

    Task<DynamicDocumentProcessDefinition> CreateDynamicDocumentDefinitionAsync(IDocumentProcessDefinition documentProcessDefinition);
}