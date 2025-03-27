using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    /// <summary>
    /// Controller for the copilot agent framework.
    /// </summary>
    [Route($"/api/copilot-agent")]
    public class CopilotAgentController : BaseController
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly IKernelFactory _kernelFactory;

        /// <inheritdoc />
        public CopilotAgentController(
            DocGenerationDbContext dbContext,
            IMapper mapper,
            IServiceProvider sp,
            IKernelFactory kernelFactory
            )
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _kernelFactory = kernelFactory;
        }

        /// <summary>
        /// Returns the document processes for a domain group.
        /// </summary>
        /// <param name="domainGroupId"></param>
        /// <returns></returns>
        [HttpGet("{domainGroupId:guid}/document-processes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        [Produces<List<DocumentProcessInfo>>]
        [EndpointName("GetDocumentProcesses")]
        [EndpointDescription("Returns the document processes for the domain group")]
        public async Task<ActionResult<DocumentProcessInfo>> GetDocumentProcesses(Guid domainGroupId)
        {
            var documentProcesses = _dbContext.DomainGroups
                .Where(x => x.Id == domainGroupId)
                .SelectMany(x => x.DocumentProcesses)
                .AsNoTracking()
                .AsSplitQuery()
                ;

            var documentProcessInfos = await _mapper.ProjectTo<DocumentProcessInfo>(documentProcesses).ToListAsync();
            return Ok(documentProcessInfos);
        }

        /// <summary>
        /// Processes a query against all document processes in a domain group,
        /// then summarizes the results into a single response.
        /// </summary>
        /// <param name="domainGroupId"></param>
        /// <param name="query"></param>
        [HttpPost("{domainGroupId:guid}/query/{query}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        [EndpointName("ProcessQuery")]
        [EndpointDescription("Processes a query against all document processes in the domain group, then summarizes the results into a single response.")]
        public async Task<ActionResult<string>> ProcessQuery(Guid domainGroupId, string query)
        {
            var documentProcesses = _dbContext.DomainGroups
                .Where(x => x.Id == domainGroupId)
                .SelectMany(x => x.DocumentProcesses)
                .AsNoTracking()
                .AsSplitQuery();

            var systemPrompt = $"""
                                    Please answer the queries provided by the user.

                                    When calling plugins for a query, make sure to take note of the document process name, which may be required input.
                                    """;

            var kernelDictionary = new Dictionary<string, Kernel>();
            var resultDictionary = new ConcurrentDictionary<string, string>();

            foreach (var dp in documentProcesses)
            {
                var dpKernel = await _kernelFactory.GetKernelForDocumentProcessAsync(dp.ShortName);
                kernelDictionary.Add(dp.ShortName, dpKernel);
            }

            var actionBlock = new ActionBlock<KeyValuePair<string, Kernel>>(async kernelDictionaryItem =>
            {
                var openAiSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(kernelDictionaryItem.Key, AiTaskType.ChatReplies);
                var dpQuery = "Query for DocumentProcessName" + kernelDictionaryItem.Key + ":\n" + query;
                var kernel = kernelDictionaryItem.Value;
                kernel.Data["DocumentProcessName"] = kernelDictionaryItem.Key;
                var result = await kernel.InvokePromptAsync(dpQuery, new KernelArguments(openAiSettings));

                resultDictionary.TryAdd(kernelDictionaryItem.Key, result.ToString());
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 3 });

            foreach (var kernelDictionaryItem in kernelDictionary)
            {
                actionBlock.Post(kernelDictionaryItem);
            }

            actionBlock.Complete();
            await actionBlock.Completion;

            var fullText = "";
            foreach (var resultDictionaryItem in resultDictionary)
            {
                fullText += $"{resultDictionaryItem.Key}:\n{resultDictionaryItem.Value}\n\n";
            }

            var summarizePrompt = $"""
                                       Here is a set of results from several queries. Can you please summarize them 
                                       into a single response?

                                       Don't mention that this is a summarized response. Don't mention or sort into document processes. Treat the
                                       result as one unified result without reference to any specific document process.

                                       If you were ask to provide sample text for a section of a document, please respond
                                       only with the text that would be included in the document.
                                       
                                       [RESULTS]
                                       {fullText}
                                       [/RESULTS]
                                       """;

            // We get a kernel for the first document process in the dictionary to use for summarization
            var summarizeKernel = await _kernelFactory.GetKernelForDocumentProcessAsync(kernelDictionary.Keys.First());

            // Plugins aren't used for summarization, so we clear them out
            summarizeKernel.Plugins.Clear();

            // We get the prompt execution settings for the document process - for task type summarization
            var openAiSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(
                kernelDictionary.Keys.First(), AiTaskType.Summarization);
            
            var summarizeResult = await summarizeKernel.InvokePromptAsync(summarizePrompt, new KernelArguments(openAiSettings));

            return Ok(summarizeResult.ToString());
        }
    }
}