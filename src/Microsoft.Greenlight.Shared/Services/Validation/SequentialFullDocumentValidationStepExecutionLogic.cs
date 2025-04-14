using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Validation.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Validation;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Validation;
using Microsoft.SemanticKernel;

namespace Microsoft.Greenlight.Shared.Services.Validation
{
    /// <summary>
    /// Logic for executing a validation step that validates the entire document with all sections sequentially.
    /// </summary>
    public class SequentialFullDocumentValidationStepExecutionLogic : IValidationStepExecutionLogic
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly ILogger<SequentialFullDocumentValidationStepExecutionLogic> _logger;
        private readonly IContentNodeService _contentNodeService;
        private readonly IServiceProvider _sp;
        private readonly IKernelFactory _kernelFactory;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly IMapper _mapper;
        private readonly IClusterClient _clusterClient;

        public SequentialFullDocumentValidationStepExecutionLogic(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<SequentialFullDocumentValidationStepExecutionLogic> logger,
            IContentNodeService contentNodeService,
            IServiceProvider sp,
            IKernelFactory kernelFactory,
            IDocumentProcessInfoService documentProcessInfoService,
            IMapper mapper,
            IClusterClient clusterClient)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _contentNodeService = contentNodeService ?? throw new ArgumentNullException(nameof(contentNodeService));
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _kernelFactory = kernelFactory ?? throw new ArgumentNullException(nameof(kernelFactory));
            _documentProcessInfoService = documentProcessInfoService ?? throw new ArgumentNullException(nameof(documentProcessInfoService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _clusterClient = clusterClient ?? throw new ArgumentNullException(nameof(clusterClient));
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(ExecuteValidationStep stepMessage)
        {
            if (stepMessage == null)
            {
                throw new ArgumentNullException(nameof(stepMessage));
            }

            _logger.LogInformation("Starting sequential validation execution for step {StepId}",
                stepMessage.ValidationPipelineExecutionStepId);

            // Create a new dependency scope for this execution
            using var scope = _sp.CreateScope();
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            try
            {
                // Load validation execution step with necessary includes
                var validationExecutionStep = await LoadValidationExecutionStepAsync(dbContext, stepMessage.ValidationPipelineExecutionStepId);

                if (validationExecutionStep == null)
                {
                    throw new InvalidOperationException(
                        $"Validation step with ID {stepMessage.ValidationPipelineExecutionStepId} not found");
                }

                // Extract required entities and validate they exist
                var (validationPipelineExecution, documentProcess, documentProcessInfo) =
                    await ExtractAndValidateEntitiesAsync(validationExecutionStep);

                // Get document and content nodes
                var documentId = validationPipelineExecution.GeneratedDocumentId;
                var document = await dbContext.GeneratedDocuments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == documentId);

                if (document == null)
                {
                    throw new InvalidOperationException($"Document with ID {documentId} not found");
                }

                // Initialize kernel for validation
                var kernel = await InitializeValidationKernelAsync(documentProcess.ShortName);
                if (kernel == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to initialize Semantic Kernel for validation of document process {documentProcess.ShortName}");
                }

                // Get document content
                var contentNodes = await _contentNodeService.GetContentNodesHierarchicalAsyncForDocumentId(
                    documentId, addParentNodes: true);

                if (contentNodes == null || contentNodes.Count == 0)
                {
                    throw new InvalidOperationException($"No content nodes found for document with ID {documentId}");
                }

                var fullText = await _contentNodeService.GetRenderedTextForContentNodeHierarchiesAsync(contentNodes);
                if (string.IsNullOrWhiteSpace(fullText))
                {
                    throw new InvalidOperationException($"Unable to render full text for document with ID {documentId}");
                }

                // Extract body text nodes from content hierarchy
                var bodyTextNodes = await ExtractBodyTextNodesFromHierarchyAsync(contentNodes);
                var sectionContents = CreateSectionAndBodyTextContents(bodyTextNodes);

                // Set up kernel arguments for validation
                var executionSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(
                    documentProcessInfo, AiTaskType.Validation);
                var kernelArguments = new KernelArguments(executionSettings);

                // Get or create validation step result
                var validationStepResult = await GetOrCreateValidationStepResultAsync(
                    dbContext, stepMessage.ValidationPipelineExecutionStepId);

                // Process each section sequentially
                await ProcessSectionsSequentiallyAsync(
                    dbContext, kernel, kernelArguments, fullText, sectionContents, bodyTextNodes,
                    validationStepResult, validationExecutionStep, validationPipelineExecution);

                _logger.LogInformation("Completed sequential validation execution for step {StepId}",
                    stepMessage.ValidationPipelineExecutionStepId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sequential document validation for step {StepId}",
                    stepMessage.ValidationPipelineExecutionStepId);
                throw;
            }
        }

        private async Task<ValidationPipelineExecutionStep?> LoadValidationExecutionStepAsync(
            DocGenerationDbContext dbContext, Guid stepId)
        {
            return await dbContext.ValidationPipelineExecutionSteps
                .Where(x => x.Id == stepId)
                .Include(x => x.ValidationPipelineExecution)
                    .ThenInclude(x => x!.DocumentProcessValidationPipeline)
                        .ThenInclude(x => x!.DocumentProcess)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync();
        }

        private async Task<(ValidationPipelineExecution, dynamic, DocumentProcessInfo)> ExtractAndValidateEntitiesAsync(
            ValidationPipelineExecutionStep validationExecutionStep)
        {
            var validationPipelineExecution = validationExecutionStep.ValidationPipelineExecution;
            var documentProcess = validationPipelineExecution?.DocumentProcessValidationPipeline?.DocumentProcess;

            if (validationPipelineExecution == null)
            {
                throw new InvalidOperationException(
                    $"Validation pipeline execution not found for validation step with ID {validationExecutionStep.Id}");
            }

            if (documentProcess == null)
            {
                throw new InvalidOperationException(
                    $"Document process not found for validation step with ID {validationExecutionStep.Id}");
            }

            var documentProcessInfo = await _documentProcessInfoService.GetDocumentProcessInfoByIdAsync(documentProcess.Id);
            if (documentProcessInfo == null)
            {
                throw new InvalidOperationException(
                    $"Document process info not found for document process with ID {documentProcess.Id}");
            }

            return (validationPipelineExecution, documentProcess, documentProcessInfo);
        }

        private async Task<Kernel> InitializeValidationKernelAsync(string documentProcessShortName)
        {
            try
            {
                // First try to get a validation-specific kernel for this document process
                var kernel = await _kernelFactory.GetValidationKernelForDocumentProcessAsync(documentProcessShortName);
                return kernel;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to get validation-specific kernel for document process {DocumentProcess}, falling back to general kernel",
                    documentProcessShortName);

                // Fall back to the general kernel factory
                return await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcessShortName);
            }
        }

        private async Task<ValidationPipelineExecutionStepResult> GetOrCreateValidationStepResultAsync(
            DocGenerationDbContext dbContext, Guid stepId)
        {
            // Check if we already have a validation step result for this step
            var existingValidationStepResult = await dbContext.ValidationPipelineExecutionStepResults
                .FirstOrDefaultAsync(x => x.ValidationPipelineExecutionStepId == stepId);

            if (existingValidationStepResult != null)
            {
                return existingValidationStepResult;
            }

            // Create a new validation step result
            var newValidationStepResult = new ValidationPipelineExecutionStepResult()
            {
                ValidationPipelineExecutionStepId = stepId,
            };

            await dbContext.ValidationPipelineExecutionStepResults.AddAsync(newValidationStepResult);
            await dbContext.SaveChangesAsync();

            return newValidationStepResult;
        }

        private List<SectionAndBodyTextContent> CreateSectionAndBodyTextContents(List<ContentNode> bodyTextNodes)
        {
            var sectionContents = new List<SectionAndBodyTextContent>();

            foreach (var bodyTextNode in bodyTextNodes)
            {
                var parentSection = bodyTextNode.Parent;
                if (parentSection == null)
                {
                    continue;
                }

                sectionContents.Add(new SectionAndBodyTextContent
                {
                    ParentSectionId = parentSection.Id,
                    BodyTextId = bodyTextNode.Id,
                    SectionText = parentSection.Text,
                    BodyText = bodyTextNode.Text
                });
            }

            return sectionContents;
        }

        private async Task ProcessSectionsSequentiallyAsync(
            DocGenerationDbContext dbContext,
            Kernel kernel,
            KernelArguments kernelArguments,
            string fullText,
            List<SectionAndBodyTextContent> sectionContents,
            List<ContentNode> bodyTextNodes,
            ValidationPipelineExecutionStepResult validationStepResult,
            ValidationPipelineExecutionStep validationExecutionStep,
            ValidationPipelineExecution validationPipelineExecution)
        {
            // Process each section sequentially
            foreach (var sectionContent in sectionContents)
            {
                try
                {
                    _logger.LogInformation("Validating section: {SectionTitle}", sectionContent.SectionText);

                    // Validate the section content
                    await ValidateSectionAsync(kernel, kernelArguments, fullText, sectionContent);

                    // Find the corresponding body text node
                    var bodyTextNode = bodyTextNodes.FirstOrDefault(x => x.Id == sectionContent.BodyTextId);
                    if (bodyTextNode == null)
                    {
                        _logger.LogWarning("Body text node {NodeId} not found, skipping result creation",
                            sectionContent.BodyTextId);
                        continue;
                    }

                    // Process validation results for this section
                    var resultantContentNodeId = await ProcessSectionValidationResultAsync(
                        dbContext, sectionContent, bodyTextNode);

                    // Create content node result record
                    var bodyTextContentNodeResult = new ValidationExecutionStepContentNodeResult
                    {
                        OriginalContentNodeId = bodyTextNode.Id,
                        ResultantContentNodeId = resultantContentNodeId,
                        ValidationPipelineExecutionStepId = validationExecutionStep.Id,
                    };

                    bodyTextContentNodeResult.ApplicationStatus = bodyTextNode.Id != resultantContentNodeId
                        ? ValidationContentNodeApplicationStatus.Unapplied              // Changes recommended - set to unapplied
                        : ValidationContentNodeApplicationStatus.NoChangesRecommended;  // No changes recommended

                    await dbContext.ValidationExecutionStepContentNodeResults.AddAsync(bodyTextContentNodeResult);
                    validationStepResult.ContentNodeResults.Add(bodyTextContentNodeResult);
                    await dbContext.SaveChangesAsync();

                    // Send notifications if content was changed
                    await SendChangeNotificationsIfNeededAsync(
                        bodyTextContentNodeResult, validationPipelineExecution);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other sections
                    _logger.LogError(ex, "Error validating section {SectionTitle}", sectionContent.SectionText);
                }
            }
        }

        private async Task<Guid> ProcessSectionValidationResultAsync(
            DocGenerationDbContext dbContext,
            SectionAndBodyTextContent sectionContent,
            ContentNode bodyTextNode)
        {
            // If there are no revisions, use the original node ID
            if (!sectionContent.HasRevisions || string.IsNullOrEmpty(sectionContent.RevisedBodyText))
            {
                return bodyTextNode.Id;
            }

            // Create a new content node for the revised text
            var revisedBodyTextNode = new ContentNode
            {
                Id = Guid.NewGuid(),
                Text = sectionContent.RevisedBodyText,
                Type = ContentNodeType.BodyText,
                // Copy any other necessary properties from the original node
                AssociatedGeneratedDocumentId = bodyTextNode.AssociatedGeneratedDocumentId,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow
            };

            await dbContext.ContentNodes.AddAsync(revisedBodyTextNode);
            await dbContext.SaveChangesAsync();

            return revisedBodyTextNode.Id;
        }

        private async Task SendChangeNotificationsIfNeededAsync(
            ValidationExecutionStepContentNodeResult bodyTextContentNodeResult,
            ValidationPipelineExecution validationPipelineExecution)
        {
            if (bodyTextContentNodeResult.ChangesRequested)
            {
                var validationNotificationMessage = new ValidationExecutionForDocumentNotification(
                    validationPipelineExecution.Id, validationPipelineExecution.GeneratedDocumentId)
                {
                    NotificationType = ValidationExecutionStatusNotificationType.ValidationStepContentChangeRequested,
                    ContentNodeChangeResult = _mapper.Map<ValidationExecutionStepContentNodeResultInfo>(bodyTextContentNodeResult)
                };

                // Use ValidationNotifierGrain to send the notification
                var notifierGrain = _clusterClient.GetGrain<IValidationNotifierGrain>(Guid.Empty);
                await notifierGrain.NotifyValidationExecutionForDocumentAsync(validationNotificationMessage);
            }
        }

        private async Task<string> ValidateSectionAsync(
            Kernel kernel,
            KernelArguments kernelArguments,
            string fullText,
            SectionAndBodyTextContent sectionContent)
        {
            // Create a comprehensive and clear prompt for validation
            var prompt = $"""
                          Given this full document text:
                          [DocumentText]
                          {fullText}
                          [/DocumentText]

                          Ensure that the text of the section:
                          {sectionContent.SectionText}
                          With the following section body text:
                          [SectionBodyText]
                          {sectionContent.BodyText}
                          [/SectionBodyText]

                          makes sense and is accurate. If you find any issues, please correct them.
                          Do not remove information, rather expand on what is already there. Especially 
                          don't remove subsections inside the section you're working on. You should, however
                          rewrite flowery language to be more succinct.
                          
                          Pay special attention to the following:
                          * Inconsistencies with other parts of the document - HIGH PRIORITY
                          * Inaccuracies or incorrect information - HIGH PRIORITY
                          * Sections repeated (same heading several times in a section) - typically results of multi-step processing. 
                            These sections should be combined and the flow of the section streamlined.
                          * Grammatical errors
                          * Spelling errors
                          * Expand on content that seems brief. You can use your available tools to help expand on content as well.
                          
                          Your ultimate goal is to make sure the section aligns well with the rest of the document.

                          Respond only with 'Yes' if the text is clear and accurate and doesn't require changes. 
                          If you find any issues, please rewrite the text to be more clear and concise.
                          If you rewrite the text, begin your response with 'Rewrite:' followed by a new line and the revised text. Don't add any
                          additional comments.
                          """;

            try
            {
                _logger.LogDebug("Sending section to validation kernel: {SectionTitle}", sectionContent.SectionText);

                var result = await kernel.InvokePromptAsync(
                    promptTemplate: prompt,
                    arguments: kernelArguments);

                string resultText = result.ToString();

                if (resultText == "Yes")
                {
                    _logger.LogInformation(
                        "No changes required to section {SectionTitle}", sectionContent.SectionText);
                    return resultText;
                }

                if (resultText.StartsWith("Rewrite:"))
                {
                    var revisedText = resultText.Replace("Rewrite:", "").Trim();
                    _logger.LogInformation(
                        "Changes required to section {SectionTitle} - adding to ValidationResult",
                        sectionContent.SectionText);
                    sectionContent.RevisedBodyText = revisedText;
                }

                return resultText;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error occurred while validating section {SectionTitle} - skipping validation",
                    sectionContent.SectionText);
                sectionContent.RevisedBodyText = null;
                return string.Empty;
            }
        }

        private async Task<List<ContentNode>> ExtractBodyTextNodesFromHierarchyAsync(List<ContentNode> contentNodes)
        {
            var bodyTextNodes = new List<ContentNode>();

            foreach (var node in contentNodes)
            {
                if (node.Type == ContentNodeType.BodyText)
                {
                    bodyTextNodes.Add(node);
                }

                if (node.Children != null && node.Children.Any())
                {
                    var childBodyTextNodes = await ExtractBodyTextNodesFromContentNodeHierarchyAsync(node.Children);
                    bodyTextNodes.AddRange(childBodyTextNodes);
                }
            }

            return bodyTextNodes;
        }

        private async Task<List<ContentNode>> ExtractBodyTextNodesFromContentNodeHierarchyAsync(List<ContentNode> contentNodes)
        {
            // This can be changed to actual async if needed
            return await Task.FromResult(ExtractBodyTextNodesRecursive(contentNodes));
        }

        private List<ContentNode> ExtractBodyTextNodesRecursive(List<ContentNode> contentNodes)
        {
            var bodyTextNodes = new List<ContentNode>();

            foreach (var node in contentNodes)
            {
                if (node.Type == ContentNodeType.BodyText)
                {
                    bodyTextNodes.Add(node);
                }

                if (node.Children != null && node.Children.Any())
                {
                    bodyTextNodes.AddRange(ExtractBodyTextNodesRecursive(node.Children));
                }
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
