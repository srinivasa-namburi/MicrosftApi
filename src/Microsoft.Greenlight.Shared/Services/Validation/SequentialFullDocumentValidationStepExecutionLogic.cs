using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Validation;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenAI.Chat;

#pragma warning disable SKEXP0010
namespace Microsoft.Greenlight.Shared.Services.Validation
{
    /// <summary>
    /// Logic for executing a validation step that validates the entire document with all sections sequentially.
    /// </summary>
    public class SequentialFullDocumentValidationStepExecutionLogic : IValidationStepExecutionLogic
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly ILogger<SequentialFullDocumentValidationStepExecutionLogic> _logger;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IContentNodeService _contentNodeService;
        private readonly IServiceProvider _sp;
        private readonly IKernelFactory _kernelFactory;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;

        public SequentialFullDocumentValidationStepExecutionLogic(
            DocGenerationDbContext dbContext,
            ILogger<SequentialFullDocumentValidationStepExecutionLogic> logger,
            IPublishEndpoint publishEndpoint,
            IContentNodeService contentNodeService,
            IServiceProvider sp, 
            IKernelFactory kernelFactory, 
            IDocumentProcessInfoService documentProcessInfoService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _publishEndpoint = publishEndpoint;
            _contentNodeService = contentNodeService;
            _sp = sp;
            _kernelFactory = kernelFactory;
            _documentProcessInfoService = documentProcessInfoService;
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(ExecuteValidationStep stepMessage)
        {
            var validationExecutionStep = _dbContext.ValidationPipelineExecutionSteps
                .Where(x => x.Id == stepMessage.ValidationPipelineExecutionStepId)
                .Include(x => x.ValidationPipelineExecution)
                .ThenInclude(x => x.DocumentProcessValidationPipeline)
                .ThenInclude(x => x.DocumentProcess)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefault();

            if (validationExecutionStep == null)
            {
                throw new InvalidOperationException(
                    $"Validation step with ID {stepMessage.ValidationPipelineExecutionStepId} not found");
            }

            var validationPipelineExecution = validationExecutionStep.ValidationPipelineExecution;
            var documentProcess = validationPipelineExecution?.DocumentProcessValidationPipeline?.DocumentProcess;


            if (documentProcess == null)
            {
                throw new InvalidOperationException(
                    $"Document process not found for validation step with ID {stepMessage.ValidationPipelineExecutionStepId}");
            }

            var documentProcessInfo = await _documentProcessInfoService.GetDocumentProcessInfoByIdAsync(documentProcess.Id);

            if (documentProcessInfo == null)
            {
                throw new InvalidOperationException(
                    $"Document process info not found for document process with ID {documentProcess.Id}");
            }

            if (validationPipelineExecution == null)
            {
                throw new InvalidOperationException(
                    $"Validation pipeline execution not found for validation step with ID {stepMessage.ValidationPipelineExecutionStepId}");
            }

            var kernel = _sp.GetValidationSemanticKernelForDocumentProcess(documentProcess.ShortName);

            if (kernel == null)
            {
                throw new InvalidOperationException(
                    $"Semantic Kernel for Validation not found for document process with short name {documentProcess.ShortName}");
            }

            var documentId = validationPipelineExecution.GeneratedDocumentId;
            var document = await _dbContext.GeneratedDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                throw new InvalidOperationException($"Document with ID {documentId} not found");
            }

            var contentNodes =
                await _contentNodeService.GetContentNodesHierarchicalAsyncForDocumentId(documentId,
                    addParentNodes: true);

            if (contentNodes == null || contentNodes.Count == 0)
            {
                throw new InvalidOperationException($"No content nodes found for document with ID {documentId}");
            }

            var fullText = await _contentNodeService.GetRenderedTextForContentNodeHierarchiesAsync(contentNodes);

            if (string.IsNullOrWhiteSpace(fullText))
            {
                throw new InvalidOperationException($"Unable to render full text for document with ID {documentId}");
            }

            // Traverse the hierarchy and pull out every body text node
            // Use recursive methods to flatten the hierarchy
            var bodyTextNodes = await ExtractBodyTextNodesFromContentNodeHierarchyAsync(contentNodes);
            // Create a list of SectionAndBodyTextContent objects - using each body text node and its parent section
            // This will allow us to validate each body text node in the context of its parent section

            var sectionAndBodyTextContents = new List<SectionAndBodyTextContent>();

            foreach (var bodyTextNode in bodyTextNodes)
            {
                var parentSection = bodyTextNode.Parent;
                if (parentSection == null)
                {
                    continue;
                }

                var sectionText = parentSection.Text;
                var bodyText = bodyTextNode.Text;

                sectionAndBodyTextContents.Add(new SectionAndBodyTextContent
                {
                    ParentSectionId = parentSection.Id,
                    BodyTextId = bodyTextNode.Id,
                    SectionText = sectionText,
                    BodyText = bodyText
                });
            }

            var executionSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(documentProcessInfo, AiTaskType.Validation);

            var kernelArguments = new KernelArguments(executionSettings) { };

            foreach (var sectionAndBodyTextContent in sectionAndBodyTextContents)
            {
                var prompt = $"""
                              Given this full document text:
                              [DocumentText]
                              {fullText}
                              [/DocumentText]

                              Ensure that the text of the section:
                              {sectionAndBodyTextContent.SectionText}
                              With the following section body text:
                              {sectionAndBodyTextContent.BodyText}

                              Makes sense and is accurate. If you find any issues, please rewrite the text to be more clear and concise.
                              Pay special attention to the following:
                              * Inconsistencies with other parts of the document
                              * Inaccuracies or incorrect information
                              * Grammatical errors
                              * Spelling errors

                              Respond only with 'Yes' if the text is clear and accurate. If you find any issues, please rewrite the text to be more clear and concise.
                              If you rewrite the text, begin your response with 'Rewrite:' followed by a new line and the revised text. Don't add any
                              additional comments.

                              """;

                try
                {
                    var result = await kernel.InvokePromptAsync(promptTemplate: prompt, arguments: kernelArguments);

                    if (result.ToString() == "Yes")
                    {
                        _logger.LogInformation($"SequentialFullDocumentValidationStepExecutionLogic : No changes required to section {sectionAndBodyTextContent.SectionText}");
                        continue;
                    }

                    if (result.ToString().StartsWith("Rewrite:"))
                    {
                        var revisedText = result.ToString().Replace("Rewrite:", "").Trim();
                        _logger.LogInformation($"SequentialFullDocumentValidationStepExecutionLogic : Changes required to section {sectionAndBodyTextContent.SectionText} - adding to ValidationResult");
                        sectionAndBodyTextContent.RevisedBodyText = revisedText;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning("Error occurred while validating section and body text content - skipping validation");
                    _logger.LogWarning(e.Message);
                    sectionAndBodyTextContent.RevisedBodyText = null;
                }

            }

            // Create a new ValidationExecutionStepContentNodeResult for each body text node.
            // If the body text node has a revision, create a new ContentNode for it. Otherwise,
            // use the existing ContentNode.

            var validationStepResult = new ValidationPipelineExecutionStepResult()
            {
                ValidationPipelineExecutionStepId = stepMessage.ValidationPipelineExecutionStepId,
            };

            foreach (var sectionAndBodyTextContent in sectionAndBodyTextContents)
            {
                var bodyTextNode = bodyTextNodes.FirstOrDefault(x => x.Id == sectionAndBodyTextContent.BodyTextId);

                if (bodyTextNode == null)
                {
                    continue;
                }

                var resultantContentNodeId = bodyTextNode.Id;

                if (sectionAndBodyTextContent.HasRevisions && !string.IsNullOrEmpty(sectionAndBodyTextContent.RevisedBodyText))
                {
                    var revisedBodyTextNode = new ContentNode
                    {
                        Id = Guid.NewGuid(),
                        //GeneratedDocumentId = documentId, // Don't set the GeneratedDocumentId - this will be set if the change is accepted
                        //AssociatedGeneratedDocumentId = bodyTextNode.AssociatedDocumentId, // Don't set the AssociatedDocumentId - this will be set if the change is accepted
                        //ParentId = bodyTextNode.ParentId, // Don't set the Parent - this will be set if the change is accepted
                        Text = sectionAndBodyTextContent.RevisedBodyText,
                        Type = ContentNodeType.BodyText,
                    };

                    await _dbContext.ContentNodes.AddAsync(revisedBodyTextNode);
                    resultantContentNodeId = revisedBodyTextNode.Id;
                }

                var bodyTextContentNodeResult = new ValidationExecutionStepContentNodeResult
                {
                    OriginalContentNodeId = bodyTextNode.Id,
                    ResultantContentNodeId = resultantContentNodeId, // this is the revised node if it exists, otherwise the original node
                    ValidationPipelineExecutionStepId = stepMessage.ValidationPipelineExecutionStepId,

                };

                await _dbContext.ValidationExecutionStepContentNodeResults.AddAsync(bodyTextContentNodeResult);
                validationStepResult.ContentNodeResults.Add(bodyTextContentNodeResult);
            }

            await _dbContext.ValidationPipelineExecutionStepResults.AddAsync(validationStepResult);
            await _dbContext.SaveChangesAsync();

        }

        private async Task<List<ContentNode>> ExtractBodyTextNodesFromContentNodeHierarchyAsync(List<ContentNode> contentNodes)
        {
            var bodyTextNodes = new List<ContentNode>();
            foreach (var node in contentNodes)
            {
                if (node.Type == ContentNodeType.BodyText)
                {
                    bodyTextNodes.Add(node);
                }

                if (!node.Children.Any())
                {
                    continue;
                }

                var childBodyTextNodes = await ExtractBodyTextNodesFromContentNodeHierarchyAsync(node.Children);
                bodyTextNodes.AddRange(childBodyTextNodes);
            }
            return bodyTextNodes;
        }
    }

    public class SectionAndBodyTextContent
    {
        public required Guid ParentSectionId { get; set; }
        public required Guid BodyTextId { get; set; }
        public required string SectionText { get; set; }
        public required string BodyText { get; set; }
        public string? RevisedBodyText { get; set; }
        public bool HasRevisions => !string.IsNullOrWhiteSpace(RevisedBodyText);

    }
}