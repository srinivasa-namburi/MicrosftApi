using AutoMapper;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Repositories;

namespace ProjectVico.V2.Shared.Services.DocumentInfo
{
    public class DocumentProcessInfoService : IDocumentProcessInfoService
    {
        private readonly IMapper _mapper;
        private readonly DynamicDocumentProcessDefinitionRepository _repository;
        private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

        public DocumentProcessInfoService(
            IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
            IMapper mapper,
            DynamicDocumentProcessDefinitionRepository repository
        )
        {
            _mapper = mapper;
            _repository = repository;
            _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        }

        public async Task<List<DocumentProcessInfo>> GetCombinedDocumentInfoListAsync()
        {
            // Materialize the static definitions
            var staticDefinitionOptions = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses;
            var mappedStaticDefinitions = staticDefinitionOptions.Select(x => _mapper.Map<DocumentProcessInfo>(x)).ToList();

            // Retrieve and map all dynamic definitions
            var dynamicDefinitions = await _repository.GetAllDynamicDocumentProcessDefinitionsAsync();
            var mappedDynamicDefinitions = dynamicDefinitions.Select(x => _mapper.Map<DocumentProcessInfo>(x)).ToList();

            // Combine static and dynamic definitions in memory
            return mappedStaticDefinitions.Concat(mappedDynamicDefinitions).ToList();
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

            var dynamicDocumentProcess = await _repository.GetByShortNameAsync(shortName);
            if (dynamicDocumentProcess == null) return null;

            result = _mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
            return result;

        }
    }
}
