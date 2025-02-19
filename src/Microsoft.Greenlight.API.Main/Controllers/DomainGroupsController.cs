using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DomainGroups;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    /// <summary>
    /// Controller for managing domain groups.
    /// </summary>
    [Route("/api/domain-groups")]
    public class DomainGroupsController : BaseController
    {
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly DocGenerationDbContext _dbContext;
        private readonly IMapper _mapper;

        /// <inheritdoc />
        public DomainGroupsController(
            IDocumentProcessInfoService documentProcessInfoService, 
            DocGenerationDbContext dbContext,
            IMapper mapper)
        {
            _documentProcessInfoService = documentProcessInfoService;
            _dbContext = dbContext;
            _mapper = mapper;
        }

        /// <summary>
        /// Return a domain group by its ID.
        /// </summary>
        /// <param name="domainGroupId">Domain Group ID</param>
        /// <returns><see cref="DomainGroupInfo"/></returns>
        [HttpGet("{domainGroupId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        [Produces<DomainGroupInfo>]
        public ActionResult<DomainGroupInfo> GetDomainGroup(Guid domainGroupId)
        {
            var domainGroup = _dbContext.DomainGroups
                .Include(dg => dg.DocumentProcesses)
                .AsNoTracking()
                .FirstOrDefault(dg => dg.Id == domainGroupId);
           
            if (domainGroup == null)
            {
                return NotFound();
            }

            var domainGroupInfo = _mapper.Map<DomainGroupInfo>(domainGroup);

            return Ok(domainGroupInfo);
        }

        /// <summary>
        /// Return all domain groups
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        [Produces<List<DomainGroupInfo>>]
        public ActionResult<List<DomainGroupInfo>> GetDomainGroups()
        {
            var domainGroups = _dbContext.DomainGroups
                .Include(dg => dg.DocumentProcesses)
                .AsNoTracking()
                .ToList();
            var domainGroupInfos = _mapper.Map<List<DomainGroupInfo>>(domainGroups);
            return Ok(domainGroupInfos);
        }

        /// <summary>
        /// Create a new domain group.
        /// </summary>
        /// <param name="domainGroupInfo">Domain Group Info</param>
        /// <returns><see cref="DomainGroupInfo"/></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Consumes("application/json")]
        [Produces("application/json")]
        [Produces<DomainGroupInfo>]
        public async Task<ActionResult<DomainGroupInfo>> CreateDomainGroup([FromBody] DomainGroupInfo domainGroupInfo)
        {
            if (domainGroupInfo == null)
            {
                return BadRequest();
            }

            var domainGroup = _mapper.Map<DomainGroup>(domainGroupInfo);
            _dbContext.DomainGroups.Add(domainGroup);
            await _dbContext.SaveChangesAsync();

            var createdDomainGroupInfo = _mapper.Map<DomainGroupInfo>(domainGroup);
            return Created($"/api/domain-groups/{domainGroup.Id}", domainGroupInfo);
        }

        /// <summary>
        /// Update an existing domain group.
        /// </summary>
        /// <param name="domainGroupId">Domain Group ID</param>
        /// <param name="domainGroupInfo">Domain Group Info</param>
        /// <returns><see cref="DomainGroupInfo"/></returns>
        [HttpPut("{domainGroupId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Consumes("application/json")]
        [Produces("application/json")]
        [Produces<DomainGroupInfo>]
        public async Task<ActionResult<DomainGroupInfo>> UpdateDomainGroup(Guid domainGroupId, [FromBody] DomainGroupInfo? domainGroupInfo)
        {
            if (domainGroupInfo == null || domainGroupId != domainGroupInfo.Id)
            {
                return BadRequest();
            }

            var existingDomainGroup = await _dbContext.DomainGroups
                .Include(dg => dg.DocumentProcesses)
                .FirstOrDefaultAsync(x => x.Id == domainGroupId);

            if (existingDomainGroup == null)
            {
                return NotFound();
            }

            // Detach existing DocumentProcesses
            foreach (var documentProcess in existingDomainGroup.DocumentProcesses)
            {
                _dbContext.Entry(documentProcess).State = EntityState.Detached;
            }

            domainGroupInfo.DocumentProcesses = [];

            _mapper.Map(domainGroupInfo, existingDomainGroup);

            // Set the state of existing DocumentProcesses to Unchanged
            foreach (var documentProcess in existingDomainGroup.DocumentProcesses)
            {
                _dbContext.Entry(documentProcess).State = EntityState.Unchanged;
            }

            _dbContext.DomainGroups.Update(existingDomainGroup);
            await _dbContext.SaveChangesAsync();

            var updatedDomainGroupInfo = _mapper.Map<DomainGroupInfo>(existingDomainGroup);
            return Ok(updatedDomainGroupInfo);
        }


        /// <summary>
        /// Delete a domain group by its ID.
        /// </summary>
        /// <param name="domainGroupId">Domain Group ID</param>
        [HttpDelete("{domainGroupId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteDomainGroup(Guid domainGroupId)
        {
            var domainGroup = await _dbContext.DomainGroups
                .FirstOrDefaultAsync(dg => dg.Id == domainGroupId);

            if (domainGroup == null)
            {
                return NotFound();
            }
            
            _dbContext.DomainGroups.Remove(domainGroup);
            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Associate a document process with a domain group.
        /// </summary>
        /// <param name="domainGroupId">Domain Group ID</param>
        /// <param name="documentProcessId">Document Process ID</param>
        /// <returns><see cref="DocumentProcessInfo"/>The DocumentProcessInfo object with the added document process</returns>
        [HttpPost("{domainGroupId:guid}/document-processes/{documentProcessId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        [Produces<DomainGroupInfo>]
        public async Task<ActionResult> AssociateDocumentProcess(Guid domainGroupId, Guid documentProcessId)
        {
            var domainGroup = await _dbContext.DomainGroups
                .Include(dg => dg.DocumentProcesses)
                .FirstOrDefaultAsync(dg => dg.Id == domainGroupId);
            
            if (domainGroup == null)
            {
                return NotFound();
            }

            // Check if the document process is already associated with the domain group. If so, return OK.
            if (domainGroup.DocumentProcesses.Any(dp => dp.Id == documentProcessId))
            {
                return Ok();
            }
            
            // Retrieve the document process in question
            var documentProcess = await _dbContext.DynamicDocumentProcessDefinitions.FindAsync(documentProcessId);

            if (documentProcess == null)
            {
                return NotFound();
            }


            domainGroup.DocumentProcesses.Add(documentProcess);
            await _dbContext.SaveChangesAsync();

            var domainGroupInfo = _mapper.Map<DomainGroupInfo>(domainGroup);
            return Ok(domainGroupInfo);
        }

        /// <summary>
        /// Disassociate a document process from a domain group.
        /// </summary>
        /// <param name="domainGroupId">Domain Group ID</param>
        /// <param name="documentProcessId">Document Process ID</param>
        /// <returns><see cref="DocumentProcessInfo"/>The DocumentProcessInfo object with the document process removed</returns>
        [HttpDelete("{domainGroupId:guid}/document-processes/{documentProcessId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        [Produces<DomainGroupInfo>]
        public async Task<ActionResult> DisassociateDocumentProcess(Guid domainGroupId, Guid documentProcessId)
        {
            var domainGroup = await _dbContext.DomainGroups
                .Include(dg => dg.DocumentProcesses)
                .FirstOrDefaultAsync(dg => dg.Id == domainGroupId);

            if (domainGroup == null)
            {
                return NotFound();
            }
            // Check if the document process is associated with the domain group. If not, return Not Found
            if (domainGroup.DocumentProcesses.All(dp => dp.Id != documentProcessId))
            {
                return NotFound();
            }

            // Retrieve the document process in question
            var documentProcess = domainGroup.DocumentProcesses.FirstOrDefault(dp => dp.Id == documentProcessId);
            if (documentProcess == null)
            {
                return NotFound();
            }

            domainGroup.DocumentProcesses.Remove(documentProcess);
            await _dbContext.SaveChangesAsync();
            var domainGroupInfo = _mapper.Map<DomainGroupInfo>(domainGroup);
            return Ok(domainGroupInfo);
        }
    }
}