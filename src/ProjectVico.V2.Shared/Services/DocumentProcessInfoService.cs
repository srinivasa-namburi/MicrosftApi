using AutoMapper;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models.DocumentProcess;
using ProjectVico.V2.Shared.Repositories;

namespace ProjectVico.V2.Shared.Services
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

        public async Task<List<DocumentProcessInfo>> GetCombinedDocumentProcessInfoListAsync()
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

        public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName)
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

        public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByIdAsync(Guid id)
        {
            var dynamicDocumentProcess = await _repository.GetByIdAsync(id);
            if (dynamicDocumentProcess == null) return null;

            var result = _mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
            return result;
        }

        public async Task<DocumentProcessInfo> CreateDocumentProcessInfoAsync(DocumentProcessInfo documentProcessInfo)
        {
            var dynamicDocumentProcess = _mapper.Map<DynamicDocumentProcessDefinition>(documentProcessInfo);
        
            if (dynamicDocumentProcess.Id == Guid.Empty)
            {
                dynamicDocumentProcess.Id = Guid.NewGuid();
            }

            await _repository.AddAsync(dynamicDocumentProcess, saveChanges:true);

            var createdDocumentProcess = await _repository.GetByShortNameAsync(dynamicDocumentProcess.ShortName);

            if (createdDocumentProcess == null)
            {
                throw new Exception("Document process could not be created.");
            }
        
            var createdDocumentProcessInfo = _mapper.Map<DocumentProcessInfo>(createdDocumentProcess);
            return createdDocumentProcessInfo;
        }


    }
}
