using Microsoft.EntityFrameworkCore;
using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Service for managing document libraries.
    /// </summary>
    public class DocumentLibraryInfoService : IDocumentLibraryInfoService
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly IMapper _mapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentLibraryInfoService"/> class.
        /// </summary>
        /// <param name="dbContextFactory">The database context factory.</param>
        /// <param name="mapper">The mapper.</param>
        public DocumentLibraryInfoService(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory, 
            IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        /// <inheritdoc/>
        public async Task<List<DocumentLibraryInfo>> GetAllDocumentLibrariesAsync()
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var libraries = await dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                    .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();

            return _mapper.Map<List<DocumentLibraryInfo>>(libraries);
        }

        /// <inheritdoc/>
        public async Task<DocumentLibraryInfo?> GetDocumentLibraryByIdAsync(Guid id)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var library = await dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                    .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(dl => dl.Id == id);

            return library == null ? null : _mapper.Map<DocumentLibraryInfo>(library);
        }

        /// <inheritdoc/>
        public async Task<DocumentLibraryInfo?> GetDocumentLibraryByShortNameAsync(string shortName)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var library = await dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                    .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(dl => dl.ShortName == shortName);

            return library == null ? null : _mapper.Map<DocumentLibraryInfo>(library);
        }

        /// <inheritdoc/>
        public async Task<List<DocumentLibraryInfo>> GetDocumentLibrariesByProcessIdAsync(Guid processId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var libraries = await dbContext.DocumentLibraries
                 .Include(dl => dl.DocumentProcessAssociations)
                     .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                 .Where(dl => dl.DocumentProcessAssociations.Any(assoc => assoc.DynamicDocumentProcessDefinitionId == processId))
                 .AsNoTracking()
                 .AsSplitQuery()
                 .ToListAsync();

            return _mapper.Map<List<DocumentLibraryInfo>>(libraries);
        }

        /// <inheritdoc/>
        public async Task<DocumentLibraryInfo?> GetDocumentLibraryByIndexNameAsync(string documentLibraryIndexName)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var library = await dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                    .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(dl => dl.IndexName == documentLibraryIndexName);

            return library == null ? null : _mapper.Map<DocumentLibraryInfo>(library);
        }

        /// <inheritdoc/>
        public async Task<DocumentLibraryInfo> CreateDocumentLibraryAsync(DocumentLibraryInfo documentLibraryInfo)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var library = _mapper.Map<DocumentLibrary>(documentLibraryInfo);
            dbContext.DocumentLibraries.Add(library);
            await dbContext.SaveChangesAsync();

            return _mapper.Map<DocumentLibraryInfo>(library);
        }

        /// <inheritdoc/>
        public async Task<DocumentLibraryInfo> UpdateDocumentLibraryAsync(DocumentLibraryInfo documentLibraryInfo)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var library = await dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                .FirstOrDefaultAsync(dl => dl.Id == documentLibraryInfo.Id);

            if (library == null)
            {
                throw new InvalidOperationException("DocumentLibrary not found.");
            }

            // Map updated properties
            _mapper.Map(documentLibraryInfo, library);

            // Update associations
            // This does not SaveChanges, so we can do it in a transaction
            UpdateDocumentProcessAssociations(dbContext, library, documentLibraryInfo.DocumentProcessAssociations);

            await dbContext.SaveChangesAsync();

            return _mapper.Map<DocumentLibraryInfo>(library);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteDocumentLibraryAsync(Guid id)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var library = await dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                .FirstOrDefaultAsync(dl => dl.Id == id);

            if (library == null)
            {
                return false;
            }

            if (library.DocumentProcessAssociations.Any())
            {
                dbContext.DocumentLibraryDocumentProcessAssociations.RemoveRange(library.DocumentProcessAssociations);
            }

            dbContext.DocumentLibraries.Remove(library);
            await dbContext.SaveChangesAsync();

            return true;
        }

        /// <inheritdoc/>
        public async Task AssociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var associationExists = await dbContext.DocumentLibraryDocumentProcessAssociations
                .AnyAsync(assoc => assoc.DocumentLibraryId == documentLibraryId && assoc.DynamicDocumentProcessDefinitionId == documentProcessId);

            if (associationExists)
            {
                return;
            }

            var association = new DocumentLibraryDocumentProcessAssociation
            {
                DocumentLibraryId = documentLibraryId,
                DynamicDocumentProcessDefinitionId = documentProcessId
            };

            dbContext.DocumentLibraryDocumentProcessAssociations.Add(association);
            await dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task DisassociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var association = await dbContext.DocumentLibraryDocumentProcessAssociations
                .FirstOrDefaultAsync(assoc => assoc.DocumentLibraryId == documentLibraryId && assoc.DynamicDocumentProcessDefinitionId == documentProcessId);

            if (association == null)
            {
                // we don't care if the association doesn't exist - simply return
                return;
            }

            dbContext.DocumentLibraryDocumentProcessAssociations.Remove(association);
            await dbContext.SaveChangesAsync();
        }

        private void UpdateDocumentProcessAssociations(DocGenerationDbContext dbContext, DocumentLibrary library, List<DocumentLibraryDocumentProcessAssociationInfo> associationInfos)
        {
            // Remove associations not in the updated list
            var associationsToRemove = library.DocumentProcessAssociations
                .Where(assoc => associationInfos.All(info => info.Id != assoc.Id))
                .ToList();
            dbContext.DocumentLibraryDocumentProcessAssociations.RemoveRange(associationsToRemove);

            // Add or update associations
            foreach (var assocInfo in associationInfos)
            {
                var existingAssociation = library.DocumentProcessAssociations
                    .FirstOrDefault(assoc => assoc.Id == assocInfo.Id);

                if (existingAssociation == null)
                {
                    var newAssociation = _mapper.Map<DocumentLibraryDocumentProcessAssociation>(assocInfo);
                    library.DocumentProcessAssociations.Add(newAssociation);
                }
                else
                {
                    _mapper.Map(assocInfo, existingAssociation);
                }
            }

            // SaveChanges is not called here, so we can do it in a transaction
            // (it's called in the UpdateDocumentLibraryAsync method)
        }
    }
}
