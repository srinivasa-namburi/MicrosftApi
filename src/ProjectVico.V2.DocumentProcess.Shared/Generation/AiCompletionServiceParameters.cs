using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Services;

namespace ProjectVico.V2.DocumentProcess.Shared.Generation;

public class AiCompletionServiceParameters<T> where T : IAiCompletionService
{
    public IOptions<ServiceConfigurationOptions> ServiceConfigurationOptions { get; }
    public OpenAIClient OpenAIClient { get; }
    public DocGenerationDbContext DbContext { get; }
    public IServiceProvider ServiceProvider { get; }
    public ILogger<T> Logger { get; }
    public IDocumentProcessInfoService DocumentProcessInfoService { get; }
    public IPromptInfoService PromptInfoService { get; }

    public AiCompletionServiceParameters(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("openai-planner")] OpenAIClient openAIClient,
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
