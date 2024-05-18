using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Data.Sql;

namespace ProjectVico.V2.Shared.Services.DocumentInfo
{
    public class DocumentProcessInfoService : IDocumentProcessInfoService
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

        public DocumentProcessInfoService(
            DocGenerationDbContext dbContext,
            IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
            IMapper mapper
        )
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        }

        public List<DocumentProcessInfo> GetCombinedDocumentInfoList()
        {
            // Materialize the static definitions
            var staticDefinitionOptions = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses;
            var mappedStaticDefinitions = staticDefinitionOptions.Select(x => _mapper.Map<DocumentProcessInfo>(x)).ToList();

            // Retrieve and map dynamic definitions
            var dynamicDefinitions = _dbContext.DynamicDocumentProcessDefinitions.Select(x => _mapper.Map<DocumentProcessInfo>(x)).ToList();

            // Combine static and dynamic definitions in memory
            return mappedStaticDefinitions.Concat(dynamicDefinitions).ToList();
        }

        public async Task<DocumentProcessInfo?> GetDocumentInfoByShortNameAsync(string shortName)
        {
            // Retrieve and map static definitions
            var staticDefinitionOptions = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses;
            var mappedStaticDefinitions = staticDefinitionOptions.Select(x => _mapper.Map<DocumentProcessInfo>(x)).ToList();

            // Search in static definitions first
            var result = mappedStaticDefinitions.FirstOrDefault(x => x.ShortName == shortName);
            if (result != null)
            {
                return result;
            }

            // If not found in static definitions, search in dynamic definitions from the database
            result = await _dbContext.DynamicDocumentProcessDefinitions
                .Where(x => x.ShortName == shortName)
                .Select(x => _mapper.Map<DocumentProcessInfo>(x))
                .FirstOrDefaultAsync();

            return result;
        }
    }
}
