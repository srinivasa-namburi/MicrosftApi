// Copyright (c) Microsoft Corporation. All rights reserved.
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Greenlight.Extensions.Plugins.Extensions;
using Microsoft.Greenlight.Shared.Plugins;
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

        private readonly AzureOpenAIClient? _openAiClient;

        /// <summary>
        /// Creates a new SemanticKernelFactory responsible for producing appropriately configured Semantic Kernel instances
        /// per document process / validation scenario as well as generic kernels.
        /// </summary>
        /// <param name="logger">Logger.</param>
        /// <param name="serviceProvider">Root service provider.</param>
        /// <param name="serviceConfigurationOptions">Application service configuration.</param>
        /// <param name="instanceContainer">Kernel instance cache container.</param>
        /// <param name="dbContextFactory">DbContext factory for retrieving model deployment metadata.</param>
        /// <param name="openAiClient">Azure OpenAI client (keyed) used for chat &amp; embedding services.</param>
        public SemanticKernelFactory(
                ILogger<SemanticKernelFactory> logger,
                IServiceProvider serviceProvider,
                IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
                SemanticKernelInstanceContainer instanceContainer,
                IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        [FromKeyedServices("openai-planner")] AzureOpenAIClient? openAiClient
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
        public async Task<Kernel> GetKernelForDocumentProcessAsync(string documentProcessName, string? providerSubjectId)
        {
            var kernel = await GetKernelForDocumentProcessAsync(documentProcessName).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(providerSubjectId))
            {
                kernel.Data[KernelUserContextConstants.ProviderSubjectId] = providerSubjectId!;
            }
            return kernel;
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
        public async Task<Kernel> GetKernelForDocumentProcessAsync(DocumentProcessInfo documentProcess, string? providerSubjectId)
        {
            var kernel = await GetKernelForDocumentProcessAsync(documentProcess).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(providerSubjectId))
            {
                kernel.Data[KernelUserContextConstants.ProviderSubjectId] = providerSubjectId!;
            }
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
        public Task<Kernel> GetGenericKernelAsync(string modelIdentifier)
        {
            // Check if we already have a generic kernel for this model
            if (_instanceContainer.GenericKernels.TryGetValue(modelIdentifier, out var existingKernel))
            {
                // Return a new instance of the existing kernel to avoid sharing state, but keeping configuration
                var newInstanceOfExistingKernel = existingKernel.Clone();
                newInstanceOfExistingKernel.Data.Clear();
                return Task.FromResult(newInstanceOfExistingKernel);
            }

            // Create a new generic kernel with the specified model
            var kernel = CreateGenericKernel(modelIdentifier);
            _instanceContainer.GenericKernels[modelIdentifier] = kernel;
            return Task.FromResult(kernel);
        }

        /// <inheritdoc />
        public async Task<Kernel> GetDefaultGenericKernelAsync()
        {
            return await GetGenericKernelAsync("gpt-4o");
        }

        /// <inheritdoc />
        public async Task<Kernel> GetFlowKernelAsync(string? providerSubjectId = null)
        {
            const string flowKernelKey = "flow-kernel";

            // Check if we already have a Flow kernel
            if (_instanceContainer.GenericKernels.TryGetValue(flowKernelKey, out var existingKernel))
            {
                // Return a new instance to avoid sharing state
                var newInstance = existingKernel.Clone();
                newInstance.Data.Clear();

                // Set user context if provided
                if (!string.IsNullOrWhiteSpace(providerSubjectId))
                {
                    newInstance.Data[KernelUserContextConstants.ProviderSubjectId] = providerSubjectId;
                }

                return newInstance;
            }

            // Create a new Flow-specific kernel
            var kernel = await CreateFlowKernelAsync();
            _instanceContainer.GenericKernels[flowKernelKey] = kernel;

            // Set user context on the returned instance
            if (!string.IsNullOrWhiteSpace(providerSubjectId))
            {
                kernel.Data[KernelUserContextConstants.ProviderSubjectId] = providerSubjectId;
            }

            return kernel;
        }

        /// <inheritdoc />
        public async Task<AzureOpenAIPromptExecutionSettings> GetFlowPromptExecutionSettingsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Get Flow configuration to determine which model deployment to use
            var flowOptions = _serviceConfigurationOptions.GreenlightServices.Flow;

            // Find the model deployment by name
            var aiModelDeployment = await dbContext.AiModelDeployments
                .Include(x => x.AiModel)
                .FirstOrDefaultAsync(x => x.DeploymentName == flowOptions.DefaultModelDeployment);

            if (aiModelDeployment?.AiModel == null)
            {
                // Fallback to gpt-4o if Flow deployment not found
                aiModelDeployment = await dbContext.AiModelDeployments
                    .Include(x => x.AiModel)
                    .FirstOrDefaultAsync(x => x.DeploymentName == "gpt-4o");

                if (aiModelDeployment?.AiModel == null)
                {
                    throw new InvalidOperationException($"Could not find AI model deployment for Flow (tried '{flowOptions.DefaultModelDeployment}' and 'gpt-4o')");
                }
            }

            // Use the same logic as document processes but for ChatReplies task type
            var additionalSettings = GetAdditionalSettingsForTaskType(aiModelDeployment, AiTaskType.ChatReplies);

            var settings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = additionalSettings.Temperature,
                FrequencyPenalty = additionalSettings.FrequencyPenalty ?? 0.0, // Allow natural repetition in conversation
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                    autoInvoke: true,
                    options: new FunctionChoiceBehaviorOptions
                    {
                        AllowConcurrentInvocation = false,
                        AllowParallelCalls = false
                    })
            };

            // Get max tokens for chat replies task type
            int maxTokens = GetMaxTokensForTaskType(aiModelDeployment, AiTaskType.ChatReplies);
            settings.MaxTokens = maxTokens;

            // Handle new completion tokens format for models that require it
            if (ModelUsesNewMaxCompletionTokens(aiModelDeployment))
            {
                settings.SetNewMaxCompletionTokensEnabled = true;
            }

            // Add reasoning effort for reasoning models
            if (additionalSettings.ReasoningEffort.HasValue)
            {
                string reasoningEffortString = additionalSettings.ReasoningEffort.Value switch
                {
                    ChatReasoningEffortLevel.Minimal => "minimal",
                    ChatReasoningEffortLevel.Low => "low",
                    ChatReasoningEffortLevel.Medium => "medium",
                    ChatReasoningEffortLevel.High => "high",
                    _ => "medium"
                };
                settings.ReasoningEffort = reasoningEffortString;
            }

            return settings;
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

            // Centralize detection of models requiring max_completion_tokens (new) vs max_tokens (legacy)
            bool isReasoning = aiModelDeployment.AiModel.IsReasoningModel || IsGpt5Model(aiModelDeployment);
            bool useNewCompletionTokens = ModelUsesNewMaxCompletionTokens(aiModelDeployment);
            promptExecutionSettings.SetNewMaxCompletionTokensEnabled = useNewCompletionTokens;

            // Set additional settings based on the task type
            if (isReasoning)
            {
                // For reasoning models, set the reasoning effort level
                promptExecutionSettings.SetNewMaxCompletionTokensEnabled = true;
                if (additionalSettings.ReasoningEffort.HasValue)
                {
                    // Convert our local enum to string for better compatibility with different SK versions
                    string reasoningEffortString = additionalSettings.ReasoningEffort.Value switch
                    {
                        ChatReasoningEffortLevel.Minimal => "minimal",
                        ChatReasoningEffortLevel.Low => "low",
                        ChatReasoningEffortLevel.Medium => "medium",
                        ChatReasoningEffortLevel.High => "high",
                        _ => "medium"
                    };
                    promptExecutionSettings.ReasoningEffort = reasoningEffortString;
                }
            }
            else
            {
                // For non-reasoning models, set temperature and frequency penalty
                promptExecutionSettings.Temperature = additionalSettings?.Temperature;
                promptExecutionSettings.FrequencyPenalty = additionalSettings?.FrequencyPenalty;
            }

            if (toolCallingEnabled)
            {

                promptExecutionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                    autoInvoke: true,
                    options: new FunctionChoiceBehaviorOptions
                    {

                        AllowConcurrentInvocation = false,
                        AllowParallelCalls = false
                    });
            }

            return promptExecutionSettings;
        }

        /// <summary>
        /// Returns <see cref="ChatOptions"/> (Microsoft.Extensions.AI) for a document process &amp; task type derived from
        /// existing execution settings logic so there is a single source of truth for temperature / penalties / max tokens.
        /// </summary>
        /// <param name="documentProcessName">Document process short name.</param>
        /// <param name="aiTaskType">Task type driving model configuration.</param>
        public async Task<ChatOptions> GetChatOptionsForDocumentProcessAsync(string documentProcessName, AiTaskType aiTaskType)
        {
            var exec = await GetPromptExecutionSettingsForDocumentProcessAsync(documentProcessName, aiTaskType).ConfigureAwait(false);
            return MapExecutionSettingsToChatOptions(exec);
        }

        /// <summary>
        /// Maps AzureOpenAIPromptExecutionSettings (SK oriented) to ChatOptions (Extensions.AI) so downstream callers not tied to SK can use unified settings.
        /// </summary>
        private static ChatOptions MapExecutionSettingsToChatOptions(AzureOpenAIPromptExecutionSettings exec)
        {
            var options = new ChatOptions
            {
                Temperature = (float?)exec.Temperature,
                FrequencyPenalty = (float?)exec.FrequencyPenalty,
                MaxOutputTokens = exec.MaxTokens
            };
            return options;
        }

        private AiModelTaskBasedAdditionalSettings GetAdditionalSettingsForTaskType(AiModelDeployment aiModelDeployment, AiTaskType aiTaskType)
        {

            var additionalSettings = new AiModelTaskBasedAdditionalSettings();

            // Check if this is a reasoning model (includes GPT-5 series)
            bool isReasoningModel = aiModelDeployment.AiModel!.IsReasoningModel || IsGpt5Model(aiModelDeployment);

            // Reasoning models don't support Temperature and FrequencyPenalty.
            if (isReasoningModel)
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

                // Map AiModelReasoningLevel to a string value expected by the connector ("low", "medium", "high")
                // Map AiModelReasoningLevel to Azure SDK enum
                // Map AiModelReasoningLevel (reasoningLevel) to ChatReasoningEffortLevel
                var reasoningEffort = MapReasoningEffortFromReasoningLevel(reasoningLevel);
                additionalSettings.ReasoningEffort = reasoningEffort;
            }

            // If we're using a reasoning model, return the additional settings here, since 
            // temperature and frequency penalty are not supported for reasoning models.
            if (isReasoningModel)
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

        /// <summary>
        /// Helper method to determine if a deployment is using a GPT-5 model
        /// </summary>
        private static bool IsGpt5Model(AiModelDeployment deployment)
        {
            var modelName = deployment?.AiModel?.Name ?? string.Empty;
            var deploymentName = deployment?.DeploymentName ?? string.Empty;

            return modelName.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) ||
                   deploymentName.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
        }

        private static ChatReasoningEffortLevel MapReasoningEffortFromReasoningLevel(AiModelReasoningLevel reasoningLevel)
        {
            return reasoningLevel switch
            {
                AiModelReasoningLevel.Minimal => ChatReasoningEffortLevel.Minimal,
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
                AiTaskType.QuestionAnswering => true,
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

            // Create kernel with document process-specific completion service via shared factory
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(scope.ServiceProvider);

            // Check if OpenAI client is available
            if (_openAiClient == null)
            {
                _logger.LogWarning("OpenAI client not configured - creating kernel without chat completion service for document process: {DocumentProcessName}", documentProcess.ShortName);
                // Return a kernel without chat completion - the system can still start
                var limitedKernel = kernelBuilder.Build();
                await EnrichKernelWithPluginsAsync(documentProcess, limitedKernel);
                return limitedKernel;
            }

            // Use native SK AzureOpenAI connector to ensure tool-calling auto invocation works with Agents
            string deploymentName = documentProcess.AiModelDeploymentId.HasValue
                ? (GetAiModelDeploymentName(documentProcess.AiModelDeploymentId.Value) ?? _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName)
                : _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;

            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
            var skChatService = new AzureOpenAIChatCompletionService(deploymentName, _openAiClient, modelId: null, loggerFactory: loggerFactory);
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(skChatService);
            // Embedding generation provided globally; no per-kernel embedding registration.

            var kernel = kernelBuilder.Build();

            // Add required plugins to the kernel
            await EnrichKernelWithPluginsAsync(documentProcess, kernel);

            // Add function invocation filter(s)
            kernel.FunctionInvocationFilters.Add(
                scope.ServiceProvider.GetRequiredKeyedService<IFunctionInvocationFilter>("InputOutputTrackingPluginInvocationFilter"));

            kernel.FunctionInvocationFilters.Add(
                scope.ServiceProvider.GetRequiredKeyedService<IFunctionInvocationFilter>("PluginExecutionLoggingFilter"));

            // Ensure user context is available during function invocations for downstream MCP plugins
            kernel.FunctionInvocationFilters.Add(new Microsoft.Greenlight.Shared.Plugins.ProviderSubjectInjectionFilter());

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

            string deploymentName;
            if (documentProcess.AiModelDeploymentForValidationId.HasValue)
            {
                deploymentName = GetAiModelDeploymentName(documentProcess.AiModelDeploymentForValidationId.Value) ?? _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;
            }
            else if (documentProcess.AiModelDeploymentId.HasValue)
            {
                deploymentName = GetAiModelDeploymentName(documentProcess.AiModelDeploymentId.Value) ?? _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;
            }
            else
            {
                deploymentName = _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;
            }

            // Check if OpenAI client is available
            if (_openAiClient == null)
            {
                _logger.LogWarning("OpenAI client not configured - creating validation kernel without chat completion service for document process: {DocumentProcessName}", documentProcess.ShortName);
                // Return a kernel without chat completion - the system can still start
                var limitedKernel = kernelBuilder.Build();
                await EnrichKernelWithPluginsAsync(documentProcess, limitedKernel);
                return limitedKernel;
            }

            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
            var validationSkService = new AzureOpenAIChatCompletionService(deploymentName, _openAiClient, modelId: null, loggerFactory: loggerFactory);
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(validationSkService);
            // Embedding service omitted (provided globally).

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

            // Use AzureOpenAI SK connector for generic kernels as well
            // Check if OpenAI client is available
            if (_openAiClient == null)
            {
                _logger.LogWarning("OpenAI client not configured - creating generic kernel without chat completion service");
                // Return a kernel without chat completion - the system can still start
                return kernelBuilder.Build();
            }

            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
            var skChatService = new AzureOpenAIChatCompletionService(modelIdentifier, _openAiClient, modelId: null, loggerFactory: loggerFactory);
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(skChatService);
            // Embedding service omitted (provided globally).

            // Build and return the kernel (without plugins)
            var built = kernelBuilder.Build();
#pragma warning restore SKEXP0010
            return built;
        }

        /// <summary>
        /// Creates a Semantic Kernel instance specifically configured for Flow orchestration.
        /// </summary>
        private async Task<Kernel> CreateFlowKernelAsync()
        {
            _logger.LogInformation("Creating new Flow kernel for conversational orchestration");

            using var scope = _serviceProvider.CreateScope();

            // Create kernel builder
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(scope.ServiceProvider);

            // Use the Flow configuration to determine the deployment
            var flowOptions = _serviceConfigurationOptions.GreenlightServices.Flow;
            string deploymentName = !string.IsNullOrWhiteSpace(flowOptions.DefaultModelDeployment)
                ? flowOptions.DefaultModelDeployment
                : _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName;

            // Check if OpenAI client is available
            if (_openAiClient == null)
            {
                _logger.LogWarning("OpenAI client not configured - creating flow kernel without chat completion service");
                // Return a kernel without chat completion - the system can still start
                var limitedKernel = kernelBuilder.Build();
                await EnrichFlowKernelWithPluginsAsync(limitedKernel, scope.ServiceProvider);
                return limitedKernel;
            }

            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
            var flowChatService = new AzureOpenAIChatCompletionService(deploymentName, _openAiClient, modelId: null, loggerFactory: loggerFactory);
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(flowChatService);

            var kernel = kernelBuilder.Build();

            // Add Flow-appropriate plugins
            await EnrichFlowKernelWithPluginsAsync(kernel, scope.ServiceProvider);

            // Add function invocation filters for Flow
            kernel.FunctionInvocationFilters.Add(
                scope.ServiceProvider.GetRequiredKeyedService<IFunctionInvocationFilter>("InputOutputTrackingPluginInvocationFilter"));

            kernel.FunctionInvocationFilters.Add(
                scope.ServiceProvider.GetRequiredKeyedService<IFunctionInvocationFilter>("PluginExecutionLoggingFilter"));

            // Ensure user context is available during function invocations
            kernel.FunctionInvocationFilters.Add(new Microsoft.Greenlight.Shared.Plugins.ProviderSubjectInjectionFilter());

            return kernel;
        }

        /// <summary>
        /// Enriches a Flow kernel with appropriate plugins for conversational orchestration.
        /// </summary>
        private async Task EnrichFlowKernelWithPluginsAsync(Kernel kernel, IServiceProvider serviceProvider)
        {
            _logger.LogInformation("Enriching Flow kernel with conversational plugins");

            KernelPluginCollection plugins = [];

            // Add only shared/static plugins that are appropriate for general Flow conversations
            // We'll exclude document-process-specific plugins since Flow operates across processes
            await AddFlowPluginsToPluginCollectionAsync(plugins, serviceProvider);

            kernel.Plugins.Clear();
            kernel.Plugins.AddRange(plugins.ToList());

            _logger.LogInformation("Flow kernel plugins enabled: {Plugins}", string.Join(", ", plugins.Select(x => x.Name)));
        }

        /// <summary>
        /// Adds Flow-appropriate plugins to the kernel plugin collection.
        /// </summary>
        private async Task AddFlowPluginsToPluginCollectionAsync(KernelPluginCollection kernelPlugins, IServiceProvider serviceProvider)
        {
            var pluginRegistry = serviceProvider.GetRequiredService<IPluginRegistry>();

            // Get all plugins from the registry
            var allPlugins = pluginRegistry.AllPlugins;

            // Only add shared/static plugins that are appropriate for general conversation
            // Exclude document-process-specific plugins (like UniversalDocsPlugin)
            var flowPlugins = allPlugins.Where(p =>
                !p.IsDynamic &&
                !p.Key.Contains("UniversalDocsPlugin") &&
                !p.Key.Contains("KmDocsPlugin")).ToList();

            foreach (var pluginEntry in flowPlugins)
            {
                try
                {
                    string? pluginRegistrationKey = pluginEntry.PluginInstance.GetType().GetServiceKeyForPluginType();

                    if (string.IsNullOrEmpty(pluginRegistrationKey))
                    {
                        pluginRegistrationKey = pluginEntry.PluginInstance.GetType().ShortDisplayName();
                    }

                    kernelPlugins.AddFromObject(pluginEntry.PluginInstance, pluginRegistrationKey);
                    _logger.LogDebug("Added Flow plugin: {PluginKey}", pluginRegistrationKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading Flow plugin {PluginType}: {Error}",
                        pluginEntry.PluginInstance.GetType().FullName, ex.Message);
                }
            }

            await Task.CompletedTask;
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

        /// <summary>
        /// Determines whether a given deployment should use the new max_completion_tokens parameter.
        /// </summary>
        private bool ModelUsesNewMaxCompletionTokens(AiModelDeployment deployment)
        {
            // Reasoning models (o-series) use the new parameter
            if (deployment?.AiModel?.IsReasoningModel == true)
            {
                return true;
            }

            var modelName = deployment?.AiModel?.Name ?? string.Empty;
            var deploymentName = deployment?.DeploymentName ?? string.Empty;

            // GPT-5 series models use the new parameter
            if (modelName.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) ||
                deploymentName.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (modelName.StartsWith("gpt-4.1", StringComparison.OrdinalIgnoreCase) ||
                deploymentName.StartsWith("gpt-4.1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }



    /// <summary>
    /// Additional model settings resolved per task type (temperature / frequency penalty or reasoning effort).
    /// </summary>
    public class AiModelTaskBasedAdditionalSettings
    {
        /// <summary>Sampling temperature (not used for reasoning models).</summary>
        public double? Temperature { get; set; }
        /// <summary>Frequency penalty controlling repetition (not used for reasoning models).</summary>
        public double? FrequencyPenalty { get; set; }
        /// <summary>Reasoning effort for reasoning models only.</summary>
        public ChatReasoningEffortLevel? ReasoningEffort { get; set; }
    }

    // NOTE: Microsoft.Extensions.AI adapters removed; legacy SK services retained pending future migration.
}
