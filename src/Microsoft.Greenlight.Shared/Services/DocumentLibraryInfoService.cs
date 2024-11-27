using Microsoft.EntityFrameworkCore;
using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;

namespace Microsoft.Greenlight.Shared.Services
{
    public class DocumentLibraryInfoService : IDocumentLibraryInfoService
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IMapper _mapper;

        public DocumentLibraryInfoService(DocGenerationDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        public async Task<List<DocumentLibraryInfo>> GetAllDocumentLibrariesAsync()
        {
            var libraries = await _dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                    .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();

            return _mapper.Map<List<DocumentLibraryInfo>>(libraries);
        }

        public async Task<DocumentLibraryInfo?> GetDocumentLibraryByIdAsync(Guid id)
        {
            var library = await _dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                    .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(dl => dl.Id == id);

            return library == null ? null : _mapper.Map<DocumentLibraryInfo>(library);
        }

        public async Task<DocumentLibraryInfo?> GetDocumentLibraryByShortNameAsync(string shortName)
        {
            var library = await _dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                    .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(dl => dl.ShortName == shortName);

            return library == null ? null : _mapper.Map<DocumentLibraryInfo>(library);
        }

        public async Task<List<DocumentLibraryInfo>> GetDocumentLibrariesByProcessIdAsync(Guid processId)
        {
           var libraries = await _dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                    .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                .Where(dl => dl.DocumentProcessAssociations.Any(assoc => assoc.DynamicDocumentProcessDefinitionId == processId))
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();

            return _mapper.Map<List<DocumentLibraryInfo>>(libraries);
        }

        public async Task<DocumentLibraryInfo?> GetDocumentLibraryByIndexNameAsync(string documentLibraryIndexName)
        {
            var library = await _dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                    .ThenInclude(assoc => assoc.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(dl => dl.IndexName == documentLibraryIndexName);

            return library == null ? null : _mapper.Map<DocumentLibraryInfo>(library);
        }

        public async Task<DocumentLibraryInfo> CreateDocumentLibraryAsync(DocumentLibraryInfo? documentLibraryInfo)
        {
            var library = _mapper.Map<DocumentLibrary>(documentLibraryInfo);
            _dbContext.DocumentLibraries.Add(library);
            await _dbContext.SaveChangesAsync();

            return _mapper.Map<DocumentLibraryInfo>(library);
        }

        public async Task<DocumentLibraryInfo> UpdateDocumentLibraryAsync(DocumentLibraryInfo? documentLibraryInfo)
        {
            var library = await _dbContext.DocumentLibraries
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
            UpdateDocumentProcessAssociations(library, documentLibraryInfo.DocumentProcessAssociations);

            await _dbContext.SaveChangesAsync();

            return _mapper.Map<DocumentLibraryInfo>(library);
        }

        public async Task<bool> DeleteDocumentLibraryAsync(Guid id)
        {
            var library = await _dbContext.DocumentLibraries
                .Include(dl => dl.DocumentProcessAssociations)
                .FirstOrDefaultAsync(dl => dl.Id == id);

            if (library == null)
            {
                return false;
            }

            if (library.DocumentProcessAssociations.Any())
            {
                _dbContext.DocumentLibraryDocumentProcessAssociations.RemoveRange(library.DocumentProcessAssociations);
            }
            
            _dbContext.DocumentLibraries.Remove(library);
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task AssociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId)
        {
            var associationExists = await _dbContext.DocumentLibraryDocumentProcessAssociations
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

            _dbContext.DocumentLibraryDocumentProcessAssociations.Add(association);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DisassociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId)
        {
            var association = await _dbContext.DocumentLibraryDocumentProcessAssociations
                .FirstOrDefaultAsync(assoc => assoc.DocumentLibraryId == documentLibraryId && assoc.DynamicDocumentProcessDefinitionId == documentProcessId);

            if (association == null)
            {
                // we don't care if the association doesn't exist - simply return
                return;
            }
            
            _dbContext.DocumentLibraryDocumentProcessAssociations.Remove(association);
            await _dbContext.SaveChangesAsync();
        }

        private void UpdateDocumentProcessAssociations(DocumentLibrary library, List<DocumentLibraryDocumentProcessAssociationInfo> associationInfos)
        {
            // Remove associations not in the updated list
            var associationsToRemove = library.DocumentProcessAssociations
                .Where(assoc => associationInfos.All(info => info.Id != assoc.Id))
                .ToList();
            _dbContext.DocumentLibraryDocumentProcessAssociations.RemoveRange(associationsToRemove);

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
