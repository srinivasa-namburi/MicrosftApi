using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models.Configuration;
using Microsoft.Greenlight.Shared.Services.Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using OpenAI.Chat;
using System.Globalization;

#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
namespace Microsoft.Greenlight.Shared.Services
{
    /// <inheritdoc />
    public class SemanticKernelFactory : IKernelFactory
    {
        private readonly ILogger<SemanticKernelFactory> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
        private readonly SemanticKernelInstanceContainer _instanceContainer;
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;

        private readonly AzureOpenAIClient _openAiClient;

        public SemanticKernelFactory(
            ILogger<SemanticKernelFactory> logger,
            IServiceProvider serviceProvider,
            IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
            SemanticKernelInstanceContainer instanceContainer,
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            [FromKeyedServices("openai-planner")]
            AzureOpenAIClient openAiClient
            )
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _serviceConfigurationOptions = serviceConfigurationOptions.Value;
            _instanceContainer = instanceContainer;
            _dbContextFactory = dbContextFactory;
            _openAiClient = openAiClient;
        }

        /// <inheritdoc />
        public async Task<Kernel> GetKernelForDocumentProcessAsync(string documentProcessName)
        {
            using var scope = _serviceProvider.CreateScope();
            var documentProcessInfoService = scope.ServiceProvider.GetRequiredService<IDocumentProcessInfoService>();
            
            // Get document process info and create a new kernel
            var documentProcess = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
            if (documentProcess == null)
            {
                throw new InvalidOperationException($"Document process with name {documentProcessName} not found");
            }

            return await GetKernelForDocumentProcessAsync(documentProcess);
        }

        /// <inheritdoc />
        public async Task<Kernel> GetKernelForDocumentProcessAsync(DocumentProcessInfo documentProcess)
        {
            // Check if we already have a kernel for this document process
            if (_instanceContainer.StandardKernels.TryGetValue(documentProcess.ShortName, out var existingKernel))
            {
                // Return a new instance of the existing kernel to avoid sharing state, but keeping configuration
                var newInstanceOfExistingKernel = existingKernel.Clone();
                newInstanceOfExistingKernel.Data.Clear();

                // We always reset the plugin collection for the kernel through EnrichKernelWithPluginsAsync
                await EnrichKernelWithPluginsAsync(documentProcess, newInstanceOfExistingKernel);
                return newInstanceOfExistingKernel;
            }

            // Create a new kernel for this document process
            var kernel = await CreateKernelForDocumentProcessAsync(documentProcess);
            _instanceContainer.StandardKernels[documentProcess.ShortName] = kernel;
            return kernel;
        }

        /// <inheritdoc />
        public async Task<Kernel> GetValidationKernelForDocumentProcessAsync(string documentProcessName)
        {
            using var scope = _serviceProvider.CreateScope();
            var documentProcessInfoService = scope.ServiceProvider.GetRequiredService<IDocumentProcessInfoService>();

            // Get document process info and create a new validation kernel
            var documentProcess = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
            if (documentProcess == null)
            {
                throw new InvalidOperationException($"Document process with name {documentProcessName} not found");
            }

            return await GetValidationKernelForDocumentProcessAsync(documentProcess);
        }

        /// <inheritdoc />
        public async Task<Kernel> GetValidationKernelForDocumentProcessAsync(DocumentProcessInfo documentProcess)
        {
            // Check if we already have a validation kernel for this document process
            if (_instanceContainer.ValidationKernels.TryGetValue(documentProcess.ShortName, out var existingKernel))
            {
                // Return a new instance of the existing kernel to avoid sharing state, but keeping configuration
                var newInstanceOfExistingKernel = existingKernel.Clone();
                newInstanceOfExistingKernel.Data.Clear();

                // We always reset the plugin collection for the kernel through EnrichKernelWithPluginsAsync
                await EnrichKernelWithPluginsAsync(documentProcess, newInstanceOfExistingKernel);
                return newInstanceOfExistingKernel;
            }

            // Create a new validation kernel for this document process
            var kernel = await CreateValidationKernelForDocumentProcessAsync(documentProcess);
            _instanceContainer.ValidationKernels[documentProcess.ShortName] = kernel;
            return kernel;
        }

        /// <inheritdoc />
        public async Task<Kernel> GetGenericKernelAsync(string modelIdentifier)
        {
            // Check if we already have a generic kernel for this model
            if (_instanceContainer.GenericKernels.TryGetValue(modelIdentifier, out var existingKernel))
            {
                // Return a new instance of the existing kernel to avoid sharing state, but keeping configuration
                var newInstanceOfExistingKernel = existingKernel.Clone();
                newInstanceOfExistingKernel.Data.Clear();
                return newInstanceOfExistingKernel;
            }

            // Create a new generic kernel with the specified model
            var kernel = CreateGenericKernel(modelIdentifier);
            _instanceContainer.GenericKernels[modelIdentifier] = kernel;
            return kernel;
        }

        /// <inheritdoc />
        public async Task<Kernel> GetDefaultGenericKernelAsync()
        {
            return await GetGenericKernelAsync("gpt-4o");
        }

        /// <inheritdoc />
        public async Task<AzureOpenAIPromptExecutionSettings> GetPromptExecutionSettingsForDocumentProcessAsync(
            string documentProcessName, AiTaskType aiTaskType)
        {
            using var scope = _serviceProvider.CreateScope();
            var documentProcessInfoService = scope.ServiceProvider.GetRequiredService<IDocumentProcessInfoService>();

            var documentProcess = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
            if (documentProcess == null)
            {
                throw new InvalidOperationException($"Document process with name {documentProcessName} not found");
            }

            return await GetPromptExecutionSettingsForDocumentProcessAsync(documentProcess, aiTaskType);
        }

        /// <inheritdoc />
        public async Task<AzureOpenAIPromptExecutionSettings> GetPromptExecutionSettingsForDocumentProcessAsync(
            DocumentProcessInfo documentProcess, AiTaskType aiTaskType)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            Guid? aiModelDeploymentId;
            if (documentProcess.Source != ProcessSource.Static)
            {
                // For dynamic document processes, use the AI model deployment ID from the document process,
                // or fall back to the known value for gpt-4o

                if (aiTaskType == AiTaskType.Validation)
                {
                    aiModelDeploymentId = documentProcess.AiModelDeploymentForValidationId ?? 
                                          Guid.Parse("453a06c4-3ce8-4468-a7a8-7444f8352aa6", CultureInfo.InvariantCulture);
                }
                else
                {
                    aiModelDeploymentId = documentProcess.AiModelDeploymentId ?? Guid.Parse("453a06c4-3ce8-4468-a7a8-7444f8352aa6", CultureInfo.InvariantCulture);
                }
                    
            }
            else
            {
                // Set the AI model deployment ID to the known value for gpt-4o for static document processes
                aiModelDeploymentId = Guid.Parse("453a06c4-3ce8-4468-a7a8-7444f8352aa6", CultureInfo.InvariantCulture);
            }

            var aiModelDeployment = await dbContext.AiModelDeployments
                .Where(x => x.Id == aiModelDeploymentId!)
                .Include(x => x.AiModel)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync();

            if (aiModelDeployment == null)
            {
                throw new InvalidOperationException($"AI model deployment with ID {documentProcess.AiModelDeploymentId} not found");
            }

            if (aiModelDeployment.AiModel == null)
            {
                throw new InvalidOperationException($"AI model with ID {aiModelDeployment.AiModelId} not found");
            }

            int maxTokens = GetMaxTokensForTaskType(aiModelDeployment, aiTaskType);
            bool toolCallingEnabled = GetToolCallingParameterForTaskType(aiModelDeployment, aiTaskType);
            var additionalSettings = GetAdditionalSettingsForTaskType(aiModelDeployment, aiTaskType);

            var promptExecutionSettings = new AzureOpenAIPromptExecutionSettings
            {
                MaxTokens = maxTokens
            };

            // Set additional settings based on the task type
            // For reasoning models, set the reasoning effort level
            if (aiModelDeployment.AiModel.IsReasoningModel)
            {
                promptExecutionSettings.SetNewMaxCompletionTokensEnabled = true;
                if (additionalSettings.ReasoningEffort.HasValue)
                {
                    promptExecutionSettings.ReasoningEffort = additionalSettings.ReasoningEffort.Value;
                }
            }
            // For non-reasoning models, set temperature and frequency penalty (not supported for reasoning models)
            else
            {
                promptExecutionSettings.Temperature = additionalSettings?.Temperature;
                promptExecutionSettings.FrequencyPenalty = additionalSettings?.FrequencyPenalty;
            }

            if (toolCallingEnabled)
            {
                promptExecutionSettings.ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions;
            }

            return promptExecutionSettings;
        }

        private AiModelTaskBasedAdditionalSettings GetAdditionalSettingsForTaskType(AiModelDeployment aiModelDeployment, AiTaskType aiTaskType)
        {

            var additionalSettings = new AiModelTaskBasedAdditionalSettings();

            // Reasoning models don't support Temperature and FrequencyPenalty.
            if (aiModelDeployment.AiModel!.IsReasoningModel)
            {
                // If we're using a reasoning model, if there are no ReasoningSettings set, set all to AiModelReasoningLevel Medium
                // which is the default value for all task types in a new AiModelReasoningSettings object.
                aiModelDeployment.ReasoningSettings ??= new AiModelReasoningSettings();

                // Decide reasoning effort level based on the AiTaskType
                AiModelReasoningLevel reasoningLevel;
                switch (aiTaskType)
                {
                    case AiTaskType.ContentGeneration:
                        reasoningLevel = aiModelDeployment.ReasoningSettings.ReasoningLevelForContentGeneration;
                        break;
                    case AiTaskType.Summarization:
                        reasoningLevel = aiModelDeployment.ReasoningSettings.ReasoningLevelForSummarization;
                        break;
                    case AiTaskType.Validation:
                        reasoningLevel = aiModelDeployment.ReasoningSettings.ReasoningLevelForValidation;
                        break;
                    case AiTaskType.ChatReplies:
                        reasoningLevel = aiModelDeployment.ReasoningSettings.ReasoningLevelForChatReplies;
                        break;
                    case AiTaskType.QuestionAnswering:
                        reasoningLevel = aiModelDeployment.ReasoningSettings.ReasoningLevelForQuestionAnswering;
                        break;
                    case AiTaskType.General:
                    default:
                        reasoningLevel = aiModelDeployment.ReasoningSettings.ReasoningLevelGeneral;
                        break;
                }

                // Map AiModelReasoningLevel (reasoningLevel) to ChatReasoningEffortLevel
                var reasoningEffort = MapReasoningEffortFromReasoningLevel(reasoningLevel);
                additionalSettings.ReasoningEffort = reasoningEffort;
            }

            // If we're using a reasoning model, return the additional settings here, since 
            // temperature and frequency penalty are not supported for reasoning models.
            if (aiModelDeployment.AiModel!.IsReasoningModel)
            {
                return additionalSettings;
            }

            switch (aiTaskType)
            {
                case AiTaskType.ContentGeneration:
                case AiTaskType.Summarization:
                    additionalSettings.Temperature = 0.5f;
                    additionalSettings.FrequencyPenalty = 0.5f;
                    break;
                case AiTaskType.Validation:
                    additionalSettings.Temperature = 0.7f;
                    additionalSettings.FrequencyPenalty = null;
                    break;
                case AiTaskType.ChatReplies:
                case AiTaskType.QuestionAnswering:
                case AiTaskType.General:
                default:
                    additionalSettings.Temperature = 1.0f;
                    additionalSettings.FrequencyPenalty = null;
                    break;
            }

            return additionalSettings;
        }

        private ChatReasoningEffortLevel MapReasoningEffortFromReasoningLevel(AiModelReasoningLevel reasoningLevel)
        {
            return reasoningLevel switch
            {
                AiModelReasoningLevel.Low => ChatReasoningEffortLevel.Low,
                AiModelReasoningLevel.Medium => ChatReasoningEffortLevel.Medium,
                AiModelReasoningLevel.High => ChatReasoningEffortLevel.High,
                _ => ChatReasoningEffortLevel.Medium
            };

        }

        private bool GetToolCallingParameterForTaskType(AiModelDeployment aiModelDeployment, AiTaskType aiTaskType)
        {
            // For ContentGeneration and ChatReplies, return true, 
            // for all other types return false

            return aiTaskType switch
            {
                AiTaskType.ContentGeneration => true,
                AiTaskType.ChatReplies => true,
                AiTaskType.Validation => true,
                _ => false
            };

        }

        private int GetMaxTokensForTaskType(AiModelDeployment aiModelDeployment, AiTaskType aiTaskType)
        {
            // Match the property name to the AiTaskType enum value and return the Max number of tokens for that task type
            return aiTaskType switch
            {
                AiTaskType.ContentGeneration => aiModelDeployment.TokenSettings.MaxTokensForContentGeneration,
                AiTaskType.Summarization => aiModelDeployment.TokenSettings.MaxTokensForSummarization,
                AiTaskType.Validation => aiModelDeployment.TokenSettings.MaxTokensForValidation,
                AiTaskType.ChatReplies => aiModelDeployment.TokenSettings.MaxTokensForChatReplies,
                AiTaskType.QuestionAnswering => aiModelDeployment.TokenSettings.MaxTokensForQuestionAnswering,
                _ => aiModelDeployment.TokenSettings.MaxTokensGeneral
            };
        }

        /// <summary>
        /// Creates a new Semantic Kernel instance for the specified document process.
        /// </summary>
        private async Task<Kernel> CreateKernelForDocumentProcessAsync(DocumentProcessInfo documentProcess)
        {
            _logger.LogInformation("Creating new kernel for document process: {DocumentProcessName}", documentProcess.ShortName);

            using var scope = _serviceProvider.CreateScope();
            
            // Create kernel with document process-specific completion service
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(scope.ServiceProvider);

            // Add a document process-specific chat completion service
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(sp =>
            {
                // Determine the model deployment name to use - fall back to the system-wide default if not specified
                string deploymentName;

                if (documentProcess.AiModelDeploymentId.HasValue)
                {
                    // Use the AiModelDeployment if available through the document process
                    var aiModelDeployment = GetAiModelDeploymentName(documentProcess.AiModelDeploymentId.Value);
                    deploymentName = aiModelDeployment ?? _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;
                }
                else
                {
                    deploymentName = _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;
                }

                // Create the chat completion service with the document-specific model
                return new AzureOpenAIChatCompletionService(
                    deploymentName,
                    _openAiClient,
                    $"openai-chatcompletion-{documentProcess.ShortName}");
            });

            kernelBuilder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
            {
                var embeddingGenerationService = new AzureOpenAITextEmbeddingGenerationService(
                    _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName,
                    _openAiClient, $"openai-embeddinggeneration-{documentProcess.ShortName}");
                return embeddingGenerationService;

            });

            var kernel = kernelBuilder.Build();

            // Add required plugins to the kernel
            await EnrichKernelWithPluginsAsync(documentProcess, kernel);

            // Add function invocation filter
            kernel.FunctionInvocationFilters.Add(
                scope.ServiceProvider.GetRequiredKeyedService<IFunctionInvocationFilter>("InputOutputTrackingPluginInvocationFilter"));

            return kernel;
        }

        /// <summary>
        /// Creates a new Semantic Kernel instance for validation with the specified document process.
        /// </summary>
        private async Task<Kernel> CreateValidationKernelForDocumentProcessAsync(DocumentProcessInfo documentProcess)
        {
            _logger.LogInformation("Creating new validation kernel for document process: {DocumentProcessName}", documentProcess.ShortName);

            using var scope = _serviceProvider.CreateScope();
            // Create kernel with document process-specific completion service
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(scope.ServiceProvider);

            // Add a document process-specific chat completion service
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(sp =>
            {
                // Determine the model deployment name to use - fall back to the system-wide default if not specified
                string deploymentName;

                if (documentProcess.AiModelDeploymentForValidationId.HasValue)
                {
                    // Use the AiModelDeployment if available through the document process
                    var aiModelDeployment = GetAiModelDeploymentName(documentProcess.AiModelDeploymentForValidationId.Value);
                    deploymentName = aiModelDeployment ?? _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;
                }
                else if (documentProcess.AiModelDeploymentId.HasValue)
                {
                    // Use the AiModelDeployment if available through the document process
                    var aiModelDeployment = GetAiModelDeploymentName(documentProcess.AiModelDeploymentId.Value);
                    deploymentName = aiModelDeployment ?? _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;
                }
                else
                {
                    deploymentName = _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;
                }


                // Create the chat completion service with the document-specific model
                return new AzureOpenAIChatCompletionService(
                    deploymentName,
                    _openAiClient,
                    $"openai-chatvalidation-{documentProcess.ShortName}");
            });

            kernelBuilder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
            {
                var embeddingGenerationService = new AzureOpenAITextEmbeddingGenerationService(
                    _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName,
                    _openAiClient, $"openai-embeddinggeneration-validation-{documentProcess.ShortName}");
                return embeddingGenerationService;

            });

            var kernel = kernelBuilder.Build();

            // Add required plugins to the kernel
            await EnrichKernelWithPluginsAsync(documentProcess, kernel);

            return kernel;
        }

        private async Task EnrichKernelWithPluginsAsync(DocumentProcessInfo documentProcess, Kernel kernel)
        {
            _logger.LogInformation("Enriching Semantic Kernel with Plugins for Document Process {dpName}", documentProcess.ShortName);
            KernelPluginCollection plugins = [];
            await plugins.AddSharedAndDocumentProcessPluginsToPluginCollectionAsync(_serviceProvider, documentProcess);
            kernel.Plugins.Clear();
            kernel.Plugins.AddRange(plugins.ToList());
            _logger.LogInformation("Plugins enabled for this Kernel:");
            _logger.LogInformation(string.Join(", ", plugins.Select(x => x.Name)));
        }

        /// <summary>
        /// Creates a generic Semantic Kernel instance with the specified model.
        /// </summary>
        private Kernel CreateGenericKernel(string modelIdentifier)
        {
            _logger.LogInformation("Creating new generic kernel with model: {ModelIdentifier}", modelIdentifier);

            using var scope = _serviceProvider.CreateScope();
            // Create kernel builder
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(scope.ServiceProvider);

            // Add chat completion service with the specified model
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(sp =>
            {
                return new AzureOpenAIChatCompletionService(
                    modelIdentifier,
                    _openAiClient,
                    $"openai-generic-{modelIdentifier}");
            });

            kernelBuilder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
            {
                var embeddingGenerationService = new AzureOpenAITextEmbeddingGenerationService(
                    _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName,
                    _openAiClient, $"openai-generic-embeddings-{modelIdentifier}");
                return embeddingGenerationService;
            });

            // Build and return the kernel (without plugins)
            return kernelBuilder.Build();
        }

        /// <summary>
        /// Gets the deployment name for the specified AI model deployment ID.
        /// </summary>
        private string? GetAiModelDeploymentName(Guid aiModelDeploymentId)
        {
            var dbContext = _dbContextFactory.CreateDbContext();
            // TODO : This should be a service with Hybrid Cache behind it
            var aiModelDeployment = dbContext.AiModelDeployments.Where(x => x.Id == aiModelDeploymentId)
                .AsNoTracking()
                .FirstOrDefault();

            return aiModelDeployment?.DeploymentName;
        }
    }

    public class AiModelTaskBasedAdditionalSettings
    {
        public double? Temperature { get; set; }
        public double? FrequencyPenalty { get; set; }
        public ChatReasoningEffortLevel? ReasoningEffort { get; set; }
    }

    namespace Microsoft.Greenlight.Shared.Services
    {
    }
}
