using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;

public class AiCompletionServiceParameters<T> where T : IAiCompletionService
{
    public IKernelFactory KernelFactory { get; }
    public IOptionsSnapshot<ServiceConfigurationOptions> ServiceConfigurationOptions { get; }
    public AzureOpenAIClient OpenAIClient { get; }
    public IServiceProvider ServiceProvider { get; }
    public ILogger<T> Logger { get; }
    public IDocumentProcessInfoService DocumentProcessInfoService { get; }
    public IPromptInfoService PromptInfoService { get; }

    public AiCompletionServiceParameters(
        IOptionsSnapshot<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("openai-planner")] AzureOpenAIClient openAIClient,
        IServiceProvider serviceProvider,
        ILogger<T> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IPromptInfoService promptInfoService,
        IKernelFactory kernelFactory)
    {
        KernelFactory = kernelFactory;
        ServiceConfigurationOptions = serviceConfigurationOptions;
        OpenAIClient = openAIClient;
        ServiceProvider = serviceProvider;
        Logger = logger;
        DocumentProcessInfoService = documentProcessInfoService;
        PromptInfoService = promptInfoService;
    }
}
