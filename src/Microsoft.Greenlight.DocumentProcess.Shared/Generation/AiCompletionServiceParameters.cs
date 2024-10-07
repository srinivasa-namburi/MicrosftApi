using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation;

public class AiCompletionServiceParameters<T> where T : IAiCompletionService
{
    public IOptions<ServiceConfigurationOptions> ServiceConfigurationOptions { get; }
    public AzureOpenAIClient OpenAIClient { get; }
    public DocGenerationDbContext DbContext { get; }
    public IServiceProvider ServiceProvider { get; }
    public ILogger<T> Logger { get; }
    public IDocumentProcessInfoService DocumentProcessInfoService { get; }
    public IPromptInfoService PromptInfoService { get; }

    public AiCompletionServiceParameters(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("openai-planner")] AzureOpenAIClient openAIClient,
        DocGenerationDbContext dbContext,
        IServiceProvider serviceProvider,
        ILogger<T> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IPromptInfoService promptInfoService)
    {
        ServiceConfigurationOptions = serviceConfigurationOptions;
        OpenAIClient = openAIClient;
        DbContext = dbContext;
        ServiceProvider = serviceProvider;
        Logger = logger;
        DocumentProcessInfoService = documentProcessInfoService;
        PromptInfoService = promptInfoService;
    }
}
