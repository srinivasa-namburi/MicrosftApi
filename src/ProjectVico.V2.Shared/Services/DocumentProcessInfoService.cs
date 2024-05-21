using AutoMapper;
using Humanizer;
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
        private readonly DynamicDocumentProcessDefinitionRepository _documentProcessRepository;
        private readonly DocumentOutlineRepository _documentOutlineRepository;
        private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

        public DocumentProcessInfoService(
            IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
            IMapper mapper,
            DynamicDocumentProcessDefinitionRepository documentProcessRepository,
           DocumentOutlineRepository documentOutlineRepository)
        {
            _mapper = mapper;
            _documentProcessRepository = documentProcessRepository;
            _documentOutlineRepository = documentOutlineRepository;
            _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        }

        public async Task<List<DocumentProcessInfo>> GetCombinedDocumentProcessInfoListAsync()
        {
            // Materialize the static definitions
            var staticDefinitionOptions = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses;
            var mappedStaticDefinitions = staticDefinitionOptions.Select(x => _mapper.Map<DocumentProcessInfo>(x)).ToList();

            // Retrieve and map all dynamic definitions
            var dynamicDefinitions = await _documentProcessRepository.GetAllDynamicDocumentProcessDefinitionsAsync();
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

            var dynamicDocumentProcess = await _documentProcessRepository.GetByShortNameAsync(shortName);
            if (dynamicDocumentProcess == null) return null;

            result = _mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
            return result;
        }

        public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByIdAsync(Guid id)
        {
            var dynamicDocumentProcess = await _documentProcessRepository.GetByIdAsync(id);
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

            // DUMMY DATA
            var outline = new DocumentOutline
            {
                Id = Guid.NewGuid(),
                FullText = """
                           1 Chapter 1
                           2 Chapter 2
                           2.1 Section 2.1
                           2.1.1 Section 2.1.1
                           2.2 Section 2.2
                           3 Chapter 3
                           3.1 Section 3.1
                           """
            };
            // END DUMMY DATA

            dynamicDocumentProcess.DocumentOutline = outline;
            await _documentProcessRepository.AddAsync(dynamicDocumentProcess);


            var createdDocumentProcess = await _documentProcessRepository.GetByShortNameAsync(dynamicDocumentProcess.ShortName);

            if (createdDocumentProcess == null)
            {
                throw new Exception("Document process could not be created.");
            }

            var createdDocumentProcessInfo = _mapper.Map<DocumentProcessInfo>(createdDocumentProcess);
            return createdDocumentProcessInfo;
        }


    }
}
