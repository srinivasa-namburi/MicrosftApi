using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Dynamic.Services;

public class DocumentDefinitionService : IDocumentDefinitionService
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public DocumentDefinitionService(
        DocGenerationDbContext dbContext,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        IMapper mapper

    )
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }
    public IQueryable<IDocumentProcessDefinition> GetCombinedListOfDocumentDefinitionsAsync()
    {
        var staticDefinitionOptions = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses;
        var staticDefinitions = staticDefinitionOptions.Select(x => _mapper.Map<DocumentProcessDefinition>(x)).AsQueryable();

        var dynamicDefinitions = _dbContext.DynamicDocumentProcessDefinitions.AsQueryable();

        // Combine the static and dynamic definitions into a single queryable and return it
        return staticDefinitions.Concat<IDocumentProcessDefinition>(dynamicDefinitions);

    }

    public async Task<IDocumentProcessDefinition?> GetDocumentDefinitionByShortNameAsync(string shortName)
    {
        var allDefinitions = GetCombinedListOfDocumentDefinitionsAsync();

        return await allDefinitions.FirstOrDefaultAsync(x => x.ShortName == shortName);
    }

    public async Task<DynamicDocumentProcessDefinition> CreateDynamicDocumentDefinitionAsync(IDocumentProcessDefinition documentProcessDefinition)
    {
        var dynamicDocumentProcessDefinition = _mapper.Map<DynamicDocumentProcessDefinition>(documentProcessDefinition);

        _dbContext.DynamicDocumentProcessDefinitions.Add(dynamicDocumentProcessDefinition);
        await _dbContext.SaveChangesAsync();
        return dynamicDocumentProcessDefinition;
    }
}