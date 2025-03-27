using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Web.Shared.Helpers;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation.Agentic
{
    public class DocumentHistoryPlugin
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly Guid? _documentId;
        private readonly ContentNode _sectionContentNode;

        public DocumentHistoryPlugin(DocGenerationDbContext dbContext, IMapper mapper, Guid? documentId, ContentNode sectionContentNode)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _documentId = documentId;
            _sectionContentNode = sectionContentNode;
        }

        [KernelFunction(nameof(GetFullCurrentContentLevel))]
        [Description("Returns the text of the full current content level, excluding the current section being worked on.")]
        public async Task<string> GetFullCurrentContentLevel()
        {
            var parentId = _sectionContentNode.ParentId;

            if (parentId == null)
            {
                return _sectionContentNode.Text;
            }

            var siblingSectionContentNodes = await _dbContext.ContentNodes
                .Where(x => x.ParentId == parentId)
                .Include(x => x.Children)
                    .Where(x => x.Type == ContentNodeType.BodyText) // Get only one level of children - of type BodyText
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();

            var fullContentLevel = new StringBuilder();

            foreach (var siblingSectionContentNode in siblingSectionContentNodes)
            {
                foreach (var child in siblingSectionContentNode.Children)
                {
                    fullContentLevel.AppendLine(siblingSectionContentNode.Text.ToUpperInvariant() + ":");
                    fullContentLevel.AppendLine();
                    fullContentLevel.Append(child.Text);
                    fullContentLevel.AppendLine();
                }
            }

            return fullContentLevel.ToString();
        }

        [KernelFunction(nameof(GetFullDocumentSoFar))]
        [Description("Returns the text of the full document that has been produced so far, excluding the current section being worked on.")]
        public async Task<string> GetFullDocumentSoFar()
        {
            var document = await _dbContext.GeneratedDocuments
                .Include(x => x.ContentNodes)
                    .ThenInclude(x => x.Children)
                        .ThenInclude(x => x.Children)
                            .ThenInclude(x => x.Children)
                                .ThenInclude(x => x.Children)
                                    .ThenInclude(x => x.Children)
                                        .ThenInclude(x => x.Children)
                                            .ThenInclude(x => x.Children)
                                                .ThenInclude(x => x.Children)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == _documentId);

            // Map these to DocumentInfos

            if (document == null)
            {
                return "The document history was not found. Maybe this is the first node being generated in it?";
            }

            var contentNodeInfos = _mapper.Map<List<ContentNodeInfo>>(document.ContentNodes);
            ContentNodeInfoSorter.SortContentNodes(contentNodeInfos);

            var fullDocumentSoFar = new StringBuilder();
            
            foreach (var contentNode in contentNodeInfos)
            {
                AppendContentNode(fullDocumentSoFar, contentNode);
            }
        
            return fullDocumentSoFar.ToString();
        }

        private void AppendContentNode(StringBuilder builder, ContentNodeInfo node)
        {
            if (node.Type is ContentNodeType.Title or ContentNodeType.Heading)
            {
                builder.AppendLine(node.Text.ToUpperInvariant() + ":");
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine(node.Text);
                builder.AppendLine();
            }

            foreach (var child in node.Children)
            {
                AppendContentNode(builder, child);
            }
        }
    }
}