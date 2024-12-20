using System.Text;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Worker.Chat.Consumers;

/// <summary>
/// Consumer class for messages of <see cref="GenerateChatHistorySummary"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GenerateChatHistorySummaryConsumer"/> class.
/// </remarks>
/// <param name="dbContext">
/// The <see cref="DocGenerationDbContext"/>.
/// </param>
/// <param name="kernel">
/// The <see cref="Kernel"/>.
/// </param>
public class GenerateChatHistorySummaryConsumer(DocGenerationDbContext dbContext, Kernel kernel) : IConsumer<GenerateChatHistorySummary>
{
    private readonly DocGenerationDbContext _dbContext = dbContext;
    private readonly Kernel _kernel = kernel;

    /// <summary>
    /// Consumes the <see cref="GenerateChatHistorySummary"/> command and generates summaries for chat history.
    /// </summary>
    /// <param name="context">
    /// The <see cref="ConsumeContext"/> containing the <see cref="GenerateChatHistorySummary"/> command.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous consume operation.
    /// </returns>
    public async Task Consume(ConsumeContext<GenerateChatHistorySummary> context)
    {
        var message = context.Message;

        var chatMessages = _dbContext.ChatMessages
            .Where(x => x.CreatedUtc < message.SummaryTime && x.ConversationId == message.CorrelationId)
            .Include(s => s.SummarizedByConversationSummary);

        var summaries = _dbContext.ConversationSummaries
            .Where(x => x.CreatedAt < message.SummaryTime && x.ConversationId == message.CorrelationId)
            .Include(m => m.SummarizedChatMessages);

        // Find messages that have not yet been summarized
        var messagesToSummarize = chatMessages
            .Where(x => x.SummarizedByConversationSummary == null)
            .ToList();

        // If there are not enough messages to summarize, return
        if (messagesToSummarize.Count < 5)
        {
            return;
        }

        // Summarize every 5 messages
        var messagesToSummarizeInBatches = messagesToSummarize
            .Select((x, i) => new { Index = i, Value = x })
            .GroupBy(x => x.Index / 5)
            .Select(x => x.Select(v => v.Value).ToList())
            .ToList();


        foreach (var batch in messagesToSummarizeInBatches)
        {
            var conversationId = batch.First().ConversationId;
            var conversationSummary = new ConversationSummary()
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var chatMessage in batch.Where(chatMessage => chatMessage.SummarizedByConversationSummary == null))
            {
                conversationSummary.SummarizedChatMessages.Add(chatMessage);
                chatMessage.SummarizedByConversationSummary = conversationSummary;
            }

            _dbContext.ConversationSummaries.Add(conversationSummary);

            await _dbContext.SaveChangesAsync();

            var summaryText = await SummarizeChatHistoryMessages(conversationSummary.SummarizedChatMessages);
            conversationSummary.SummaryText = summaryText;

            await _dbContext.SaveChangesAsync();

        }

    }

    private async Task<string> SummarizeChatHistoryMessages(List<ChatMessage> chatHistory)
    {
        var chatHistoryBuilder = new StringBuilder();

        // For each chat message, create a string with timestamp, user/assistant, and message. Separate by | character and end with a newline
        foreach (var chatMessage in chatHistory)
        {
            chatHistoryBuilder
                .AppendLine("role:" + chatMessage.Source.ToString())
                .AppendLine("content:" + chatMessage.Message);

        }

        // Output the chat history to a string variable
        var chatHistoryString = chatHistoryBuilder.ToString();

        var prompt = $"""
                      Please summarize the following chat history:

                      [ChatHistory]
                      {chatHistoryString}
                      [/ChatHistory]
                      """;

        var openAiExecutionSettings = new OpenAIPromptExecutionSettings()
        {
            MaxTokens = 200,
            Temperature = 0.7

        };

        var kernelArguments = new KernelArguments(openAiExecutionSettings);

        var response = await _kernel.InvokePromptAsync(prompt, kernelArguments);

        return response.ToString();

    }
}
