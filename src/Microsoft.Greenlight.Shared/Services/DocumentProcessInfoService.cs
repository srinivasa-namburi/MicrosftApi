using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Prompts;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Service for managing document process information.
    /// </summary>
    public class DocumentProcessInfoService : IDocumentProcessInfoService
    {
        private readonly IMapper _mapper;
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly DefaultPromptCatalogTypes _defaultPromptCatalogTypes;
        private readonly ILogger<DocumentProcessInfoService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentProcessInfoService"/> class.
        /// </summary>
        public DocumentProcessInfoService(
            IMapper mapper,
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<DocumentProcessInfoService> logger)
        {
            _mapper = mapper;
            _dbContextFactory = dbContextFactory;
            _defaultPromptCatalogTypes = new DefaultPromptCatalogTypes();
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<DocumentProcessInfo>> GetCombinedDocumentProcessInfoListAsync()
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Only retrieve dynamic document process definitions from database
            var dynamicDefinitions = await dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .AsNoTracking()
                .ToListAsync();

            // Map them to DocumentProcessInfo objects
            return _mapper.Map<List<DocumentProcessInfo>>(dynamicDefinitions);
        }

        /// <inheritdoc/>
        public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName)
        {
            var dbContext = _dbContextFactory.CreateDbContext();

            // Get document process from the database
            var dynamicDocumentProcess = await dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShortName == shortName);

            if (dynamicDocumentProcess == null) return null;

            // Map to DocumentProcessInfo
            return _mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
        }

        public DocumentProcessInfo GetDocumentProcessInfoByShortName(string shortName)
        {
            var dbContext = _dbContextFactory.CreateDbContext();

            // Get document process from the database
            var dynamicDocumentProcess = dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .AsNoTracking()
                .FirstOrDefault(x => x.ShortName == shortName);

            if (dynamicDocumentProcess == null) return null;

            // Map to DocumentProcessInfo
            return _mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
        }

        /// <inheritdoc/>
        public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByIdAsync(Guid id)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var dynamicDocumentProcess = await dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (dynamicDocumentProcess == null) return null;

            return _mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentProcessInfo>> GetDocumentProcessesByLibraryIdAsync(Guid libraryId)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var processes = await dbContext.DocumentLibraryDocumentProcessAssociations
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
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

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

            dynamicDocumentProcess.DocumentOutline = outline;

            // Add and save the new document process
            await dbContext.DynamicDocumentProcessDefinitions.AddAsync(dynamicDocumentProcess);
            await dbContext.SaveChangesAsync();

            // Create missing prompt implementations
            await CreateMissingPromptImplementations(dynamicDocumentProcess.Id, dbContext);

            // Check if the document process was created successfully
            var createdDocumentProcess = await dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShortName == dynamicDocumentProcess.ShortName);

            if (createdDocumentProcess == null)
            {
                throw new Exception("Document process could not be created.");
            }

            var createdDocumentProcessInfo = _mapper.Map<DocumentProcessInfo>(createdDocumentProcess);
            return createdDocumentProcessInfo;
        }

        /// <summary>
        /// Creates a minimal document process without default outline or prompt implementations.
        /// This is useful for import scenarios where custom content will be added separately.
        /// </summary>
        /// <param name="documentProcessInfo">The document process information to create.</param>
        /// <returns>The created document process information.</returns>
        public async Task<DocumentProcessInfo> CreateMinimalDocumentProcessInfoAsync(DocumentProcessInfo documentProcessInfo)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            ValidateAndFormatShortName(documentProcessInfo);

            var dynamicDocumentProcess = _mapper.Map<DynamicDocumentProcessDefinition>(documentProcessInfo);
            if (dynamicDocumentProcess.Id == Guid.Empty)
            {
                dynamicDocumentProcess.Id = Guid.NewGuid();
            }

            // Set status to Active but don't create any default content
            dynamicDocumentProcess.Status = DocumentProcessStatus.Active;

            // Add and save the new document process WITHOUT outline or prompt implementations
            await dbContext.DynamicDocumentProcessDefinitions.AddAsync(dynamicDocumentProcess);
            await dbContext.SaveChangesAsync();

            // Return the created process info
            var createdDocumentProcessInfo = _mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
            return createdDocumentProcessInfo;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteDocumentProcessInfoAsync(Guid processId)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var documentProcess = await dbContext.DynamicDocumentProcessDefinitions
                .Include(p => p.Prompts)
                .Include(d => d.DocumentOutline)
                .ThenInclude(documentOutline => documentOutline!.OutlineItems)
                .ThenInclude(y => y.Children)
                .ThenInclude(v => v.Children)
                .ThenInclude(w => w.Children)
                .ThenInclude(x => x.Children)
                .FirstOrDefaultAsync(x => x.Id == processId);

            // No definition found to delete, so return false
            if (documentProcess == null)
            {
                return false;
            }

            // Recursively delete the document outline items, starting with leaf nodes
            if (documentProcess.DocumentOutline != null)
            {
                var outlineItems = documentProcess.DocumentOutline.OutlineItems;
                foreach (var item in outlineItems)
                {
                    await DeleteDocumentOutlineItem(item);
                }

                // Remove the document outline
                dbContext.DocumentOutlines.Remove(documentProcess.DocumentOutline);
            }

            // Remove the document process itself
            dbContext.DynamicDocumentProcessDefinitions.Remove(documentProcess);
            await dbContext.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Creates missing prompt implementations for a document process.
        /// </summary>
        /// <param name="documentProcessId">The ID of the document process.</param>
        /// <param name="dbContext">The database context.</param>
        private async Task CreateMissingPromptImplementations(Guid documentProcessId, DocGenerationDbContext dbContext)
        {
            var documentProcess = await dbContext.DynamicDocumentProcessDefinitions
                .Include(x => x.DocumentOutline)
                .FirstOrDefaultAsync(x => x.Id == documentProcessId);

            if (documentProcess == null)
            {
                _logger.LogWarning(
                    "DocumentProcessInfoService: Document Process with Id {DocumentProcessId} not found",
                    documentProcessId);
                return;
            }

            // Get all Prompt Implementations for the Dynamic Document Process to see if they already exist
            var promptImplementationsForDocumentProcess = await dbContext.PromptImplementations
                .Where(pi => pi.DocumentProcessDefinitionId == documentProcess.Id)
                .Include(promptImplementation => promptImplementation.PromptDefinition)
                .ToListAsync();

            // Loop through all the properties in the DefaultPromptCatalogTypes class to see if there are any missing
            // Prompt Implementations for this Document Process.
            // We expect to have a Prompt Implementation for each property in the DefaultPromptCatalogTypes class

            var numberOfPromptImplementationsAdded = 0;
            foreach (var promptCatalogProperty in _defaultPromptCatalogTypes.GetType()
                                                                            .GetProperties()
                                                                            .Where(p => p.PropertyType == typeof(string)))
            {
                var promptImplementation =
                    promptImplementationsForDocumentProcess.FirstOrDefault(pi =>
                        pi.PromptDefinition != null && pi.PromptDefinition.ShortCode == promptCatalogProperty.Name);
                if (promptImplementation == null)
                {
                    var promptDefinition = await dbContext.PromptDefinitions
                        .FirstOrDefaultAsync(pd => pd.ShortCode == promptCatalogProperty.Name);

                    if (promptDefinition != null)
                    {
                        promptImplementation = new PromptImplementation
                        {
                            DocumentProcessDefinitionId = documentProcess.Id,
                            PromptDefinitionId = promptDefinition.Id,
                            Text = promptCatalogProperty.GetValue(_defaultPromptCatalogTypes)?.ToString() ?? ""
                        };

                        _logger.LogInformation(
                            "DocumentProcessInfoService: Creating prompt implementation of prompt {PromptName} for DP {DocumentProcessShortname}",
                            promptDefinition.ShortCode,
                            documentProcess.ShortName);

                        await dbContext.PromptImplementations.AddAsync(promptImplementation);
                        numberOfPromptImplementationsAdded++;
                    }
                }
            }

            if (numberOfPromptImplementationsAdded > 0)
            {
                await dbContext.SaveChangesAsync();
            }

            documentProcess.Status = DocumentProcessStatus.Active;

            dbContext.DynamicDocumentProcessDefinitions.Update(documentProcess);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "DocumentProcessInfoService: Created {NumberOfPromptImplementationsAdded} prompt implementations for DP {DocumentProcessShortname}",
                numberOfPromptImplementationsAdded,
                documentProcess.ShortName);
        }


        private async Task DeleteDocumentOutlineItem(DocumentOutlineItem item)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            if (item.Children != null)
            {
                foreach (var child in item.Children)
                {
                    await DeleteDocumentOutlineItem(child);
                }
            }

            dbContext.DocumentOutlineItems.Remove(item);
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
