using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentProcesses;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Repositories;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Service for managing document process information.
    /// </summary>
    public class DocumentProcessInfoService : IDocumentProcessInfoService
    {
        private readonly IMapper _mapper;
        private readonly DynamicDocumentProcessDefinitionRepository _documentProcessRepository;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
        private readonly DocGenerationDbContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentProcessInfoService"/> class.
        /// </summary>
        /// <param name="serviceConfigurationOptions">The service configuration options.</param>
        /// <param name="mapper">The mapper.</param>
        /// <param name="documentProcessRepository">The document process repository.</param>
        /// <param name="publishEndpoint">The publish endpoint.</param>
        /// <param name="dbContext">The database context.</param>
        public DocumentProcessInfoService(
            IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
            IMapper mapper,
            DynamicDocumentProcessDefinitionRepository documentProcessRepository,
            IPublishEndpoint publishEndpoint, DocGenerationDbContext dbContext)
        {
            _mapper = mapper;
            _documentProcessRepository = documentProcessRepository;
            _publishEndpoint = publishEndpoint;
            _dbContext = dbContext;
            _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        }

        /// <inheritdoc/>
        public async Task<List<DocumentProcessInfo>> GetCombinedDocumentProcessInfoListAsync()
        {
            // Materialize the static definitions
            var staticDefinitionOptions = _serviceConfigurationOptions.GreenlightServices.DocumentProcesses;
            var mappedStaticDefinitions = staticDefinitionOptions.Select(x => _mapper.Map<DocumentProcessInfo>(x)).ToList();

            // Remove duplicates from the static definitions
            var distinctStaticDefinitions = mappedStaticDefinitions
                .GroupBy(x => x.ShortName)
                .Select(x => x.First())
                .ToList();

            // Retrieve and map all dynamic definitions
            var dynamicDefinitions = await _documentProcessRepository.GetAllDynamicDocumentProcessDefinitionsAsync();
            var mappedDynamicDefinitions = dynamicDefinitions.Select(x => _mapper.Map<DocumentProcessInfo>(x)).ToList();

            // Combine static and dynamic definitions in memory
            return distinctStaticDefinitions.Concat(mappedDynamicDefinitions).ToList();
        }

        /// <inheritdoc/>
        public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName)
        {
            // Retrieve and map static definitions
            var staticDefinitionOptions = _serviceConfigurationOptions.GreenlightServices.DocumentProcesses;
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

        /// <inheritdoc/>
        public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByIdAsync(Guid id)
        {
            // If we're looking for a Document Process by ID, we should only look in the dynamic definitions, as 
            // static definitions do not have an ID.

            var dynamicDocumentProcess = await _dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (dynamicDocumentProcess == null) return null;

            var result = _mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
            return result;
        }

        /// <inheritdoc/>
        public async Task<List<DocumentProcessInfo>> GetDocumentProcessesByLibraryIdAsync(Guid libraryId)
        {
            var processes = await _dbContext.DocumentLibraryDocumentProcessAssociations
                .Where(x => x.DocumentLibraryId == libraryId)
                .Include(x => x.DynamicDocumentProcessDefinition)
                    .ThenInclude(x => x!.DocumentOutline)
                        .ThenInclude(x => x!.OutlineItems)
                .Include(x => x.DocumentLibrary)
                .AsNoTracking()
                .AsSplitQuery()
                .Select(x => x.DynamicDocumentProcessDefinition)
                .ToListAsync();

            return _mapper.Map<List<DocumentProcessInfo>>(processes);
        }

        /// <inheritdoc/>
        public async Task<DocumentProcessInfo> CreateDocumentProcessInfoAsync(DocumentProcessInfo documentProcessInfo)
        {
            ValidateAndFormatShortName(documentProcessInfo);

            var dynamicDocumentProcess = _mapper.Map<DynamicDocumentProcessDefinition>(documentProcessInfo);
            if (dynamicDocumentProcess.Id == Guid.Empty)
            {
                dynamicDocumentProcess.Id = Guid.NewGuid();
            }

            var documentOutlineId = Guid.NewGuid();
            var outline = new DocumentOutline
            {
                Id = documentOutlineId,
                OutlineItems =
                [
                    new DocumentOutlineItem()
                        {
                            SectionNumber = "1",
                            SectionTitle = "Chapter 1",
                            Level = 0
                        },
                        new DocumentOutlineItem()
                        {
                            SectionNumber = "2",
                            SectionTitle = "Chapter 2",
                            Level = 0,
                            Children =
                            [
                                new DocumentOutlineItem()
                                {
                                    SectionNumber = "2.1",
                                    SectionTitle = "Section 2.1",
                                    Level = 1,
                                    Children =
                                    [
                                        new DocumentOutlineItem()
                                        {
                                            SectionNumber = "2.1.1",
                                            SectionTitle = "Section 2.1.1",
                                            Level = 2
                                        }
                                    ]
                                },
                                new DocumentOutlineItem()
                                {
                                    SectionNumber = "2.2",
                                    SectionTitle = "Section 2.2",
                                    Level = 1
                                }
                            ]
                        },
                        new DocumentOutlineItem()
                        {
                            SectionNumber = "3",
                            SectionTitle = "Chapter 3",
                            Level = 0,
                            Children =
                            [
                                new DocumentOutlineItem()
                                {
                                    SectionNumber = "3.1",
                                    SectionTitle = "Section 3.1",
                                    Level = 1
                                }
                            ]
                        }
                ]
            };

            // END DUMMY DATA

            dynamicDocumentProcess.DocumentOutline = outline;
            await _documentProcessRepository.AddAsync(dynamicDocumentProcess);

            var createdDocumentProcess = await _documentProcessRepository.GetByShortNameAsync(dynamicDocumentProcess.ShortName);

            if (createdDocumentProcess == null)
            {
                throw new Exception("Document process could not be created.");
            }

            await _publishEndpoint.Publish(new CreateDynamicDocumentProcessPrompts(createdDocumentProcess.Id));
            var createdDocumentProcessInfo = _mapper.Map<DocumentProcessInfo>(createdDocumentProcess);
            return createdDocumentProcessInfo;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteDocumentProcessInfoAsync(Guid processId)
        {
            var documentProcess = await _documentProcessRepository.GetByIdAsync(processId, false);
            if (documentProcess != null)
            {
                await _documentProcessRepository.DeleteAsync(documentProcess);
                return true;
            }
            else return false;
        }

        private void ValidateAndFormatShortName(DocumentProcessInfo documentProcessInfo)
        {
            if (string.IsNullOrWhiteSpace(documentProcessInfo.ShortName))
            {
                throw new ArgumentException("Short Name cannot be empty.");
            }

            // Replace spaces with periods
            documentProcessInfo.ShortName = documentProcessInfo.ShortName.Replace(" ", ".");

            // Remove any characters that are not letters, digits, or periods
            documentProcessInfo.ShortName = new string(documentProcessInfo.ShortName.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());

            if (string.IsNullOrWhiteSpace(documentProcessInfo.ShortName))
            {
                throw new ArgumentException("Short Name must contain valid characters.");
            }
        }
    }
}
