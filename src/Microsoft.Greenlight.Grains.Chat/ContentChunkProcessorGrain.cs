using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.RegularExpressions;


namespace Microsoft.Greenlight.Grains.Chat
{
    /// <summary>
    /// Grain for processing content updates as chunks rather than complete replacements
    /// </summary>
    public class ContentChunkProcessorGrain : Grain, IContentChunkProcessorGrain
    {
        private readonly ILogger<ContentChunkProcessorGrain> _logger;
        private readonly IKernelFactory _kernelFactory;

        // Original content for reference
        private string _originalContent;

        // Tracking of processed chunks
        private List<ContentChunk> _processedChunks = new List<ContentChunk>();

        public ContentChunkProcessorGrain(
            ILogger<ContentChunkProcessorGrain> logger,
            IKernelFactory kernelFactory)
        {
            _logger = logger;
            _kernelFactory = kernelFactory;
        }

        /// <summary>
        /// Processes content update requests in chunk mode
        /// </summary>
        public async Task ProcessContentUpdateAsync(
            Guid conversationId,
            Guid messageId,
            string originalContent,
            string userQuery,
            string documentProcessName,
            string systemPrompt)
        {
            _originalContent = originalContent;

            try
            {
                // Create an assistant message that will be updated with progress
                var assistantMessageId = Guid.NewGuid();
                var assistantMessage = new ChatMessageDTO
                {
                    Id = assistantMessageId,
                    ConversationId = conversationId,
                    Source = ChatMessageSource.Assistant,
                    ReplyToId = messageId,
                    Message = "Analyzing your content and preparing updates...",
                    State = ChatMessageCreationState.InProgress,
                    CreatedUtc = DateTime.UtcNow
                };

                // Send initial assistant message
                await SendAssistantMessageUpdateAsync(conversationId, assistantMessage);

                // Get a kernel for the document process
                var sk = await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcessName);
                var executionSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(
                    documentProcessName, AiTaskType.ChatReplies);
                    
                // Append special instructions for chunk-based processing
                var chunkPrompt = CreateChunkProcessingPrompt(originalContent, userQuery);

                // Process with AI
                var kernelArguments = new KernelArguments(executionSettings);
                _processedChunks.Clear();

                // Track processed chunks by their signature to avoid duplicates
                var processedChunkSignatures = new HashSet<string>();
                Dictionary<string, List<ContentChunk>> chunksByType = new()
                {
                    { "headings", new List<ContentChunk>() },
                    { "paragraphs", new List<ContentChunk>() },
                    { "formatting", new List<ContentChunk>() },
                    { "grammar", new List<ContentChunk>() },
                    { "other", new List<ContentChunk>() }
                };

                // Stream the response and process it to extract chunks
                StringBuilder responseBuilder = new StringBuilder();

                // Update message to indicate we're generating content improvements
                assistantMessage.Message = "Analyzing your content request:\n\n" + 
                                          "• Reviewing document structure\n" +
                                          "• Identifying improvement opportunities";
                await SendAssistantMessageUpdateAsync(conversationId, assistantMessage);

                // Process the streaming response to extract content chunks
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(7)); // Safety timeout

                try
                {
                    // Process streaming directly
                    await foreach (var chunk in sk.InvokePromptStreamingAsync(chunkPrompt, kernelArguments, cancellationToken: cts.Token))
                    {
                        responseBuilder.Append(chunk);

                        // Try to extract complete chunks from the response so far
                        var extractedChunks = ExtractContentChunks(responseBuilder.ToString(), originalContent);
                        
                        // Filter out already processed chunks using signatures
                        var newChunks = new List<ContentChunk>();
                        foreach (var extractedChunk in extractedChunks)
                        {
                            var signature = GenerateChunkSignature(extractedChunk);
                            if (!processedChunkSignatures.Contains(signature))
                            {
                                processedChunkSignatures.Add(signature);
                                newChunks.Add(extractedChunk);
                                
                                // Categorize the chunk
                                CategorizeChunk(extractedChunk, chunksByType);
                            }
                        }

                        if (newChunks.Any())
                        {
                            // Send new chunks to clients
                            await SendChunkUpdateAsync(conversationId, messageId, newChunks, false);

                            // Add to processed chunks
                            _processedChunks.AddRange(newChunks);

                            // Update the assistant message with current progress
                            assistantMessage.Message = GenerateProgressMessage(chunksByType);
                            await SendAssistantMessageUpdateAsync(conversationId, assistantMessage);

                            // Remove processed chunks from the response buffer
                            foreach (var extractedChunk in newChunks)
                            {
                                // Remove the chunk definition from the response buffer
                                var chunkPattern = CreateChunkRemovalPattern(extractedChunk);
                                var match = Regex.Match(responseBuilder.ToString(), chunkPattern);
                                if (match.Success)
                                {
                                    responseBuilder.Remove(match.Index, match.Length);
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Content processing timed out for conversation {ConversationId}, message {MessageId}",
                        conversationId, messageId);

                    assistantMessage.Message += "\n\n⚠️ Processing timed out. Applying changes generated so far.";
                    await SendAssistantMessageUpdateAsync(conversationId, assistantMessage);
                }

                // Process any remaining text for chunks
                var remainingChunks = ExtractContentChunks(responseBuilder.ToString(), originalContent);
                
                // Filter out already processed chunks
                var newRemainingChunks = remainingChunks
                    .Where(c => !processedChunkSignatures.Contains(GenerateChunkSignature(c)))
                    .ToList();
                    
                if (newRemainingChunks.Any())
                {
                    foreach (var chunk in newRemainingChunks)
                    {
                        processedChunkSignatures.Add(GenerateChunkSignature(chunk));
                        CategorizeChunk(chunk, chunksByType);
                    }
                    
                    await SendChunkUpdateAsync(conversationId, messageId, newRemainingChunks, true);
                    _processedChunks.AddRange(newRemainingChunks);

                    // Update final message
                    assistantMessage.Message = GenerateCompletionMessage(chunksByType);
                    assistantMessage.State = ChatMessageCreationState.Complete;
                    await SendAssistantMessageUpdateAsync(conversationId, assistantMessage);
                }
                else
                {
                    // Send completion notification even if no more chunks
                    await SendCompletionNotificationAsync(conversationId, messageId);

                    // Final message update
                    assistantMessage.Message = _processedChunks.Any()
                        ? GenerateCompletionMessage(chunksByType)
                        : "I've analyzed your content but didn't find any changes needed. Your content looks good as is!";
                    assistantMessage.State = ChatMessageCreationState.Complete;
                    await SendAssistantMessageUpdateAsync(conversationId, assistantMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content update for conversation {ConversationId}, message {MessageId}",
                    conversationId, messageId);

                // Create a final error message
                var errorMessage = new ChatMessageDTO
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Source = ChatMessageSource.Assistant,
                    ReplyToId = messageId,
                    Message = $"I encountered an error while processing your content update: {ex.Message}",
                    State = ChatMessageCreationState.Failed,
                    CreatedUtc = DateTime.UtcNow
                };
                
                await SendAssistantMessageUpdateAsync(conversationId, errorMessage);

                // Always send a completion notification to unblock the UI
                await SendCompletionNotificationAsync(conversationId, messageId);
            }
        }

        private void CategorizeChunk(ContentChunk chunk, Dictionary<string, List<ContentChunk>> chunksByType)
        {
            // Simple heuristics to categorize changes
            string lowerOriginal = chunk.OriginalText?.ToLower() ?? "";
            string lowerNew = chunk.NewText?.ToLower() ?? "";
            
            // Check for headings (text that starts with # or has heading HTML tags)
            if ((lowerOriginal.StartsWith("#") || lowerNew.StartsWith("#") || 
                 lowerOriginal.Contains("<h") || lowerNew.Contains("<h")) ||
                (chunk.OriginalText?.Length <= 100 && lowerOriginal.EndsWith(":") || lowerNew.EndsWith(":")))
            {
                chunksByType["headings"].Add(chunk);
                return;
            }
            
            // Check for simple formatting changes (punctuation, capitalization, minor word changes)
            if (chunk.OriginalText != null && chunk.NewText != null && 
                Math.Abs(chunk.OriginalText.Length - chunk.NewText.Length) < 5 &&
                (ContainsFormatting(lowerOriginal) || ContainsFormatting(lowerNew)))
            {
                chunksByType["formatting"].Add(chunk);
                return;
            }
            
            // Check for grammar fixes (short replacements, typical grammar issues)
            if (chunk.OriginalText != null && chunk.NewText != null && 
                Math.Abs(chunk.OriginalText.Length - chunk.NewText.Length) < 15 &&
                (ContainsGrammarIssue(lowerOriginal) || IsLikelyGrammarFix(lowerOriginal, lowerNew)))
            {
                chunksByType["grammar"].Add(chunk);
                return;
            }
            
            // Assume longer changes are paragraph rewrites
            if ((chunk.OriginalText?.Length > 100 || chunk.NewText?.Length > 100) ||
                chunk.OriginalText?.Contains("\n") == true || chunk.NewText?.Contains("\n") == true)
            {
                chunksByType["paragraphs"].Add(chunk);
                return;
            }
            
            // Default category
            chunksByType["other"].Add(chunk);
        }

        private bool ContainsFormatting(string text)
        {
            return text.Contains("*") || text.Contains("_") || text.Contains("**") || 
                   text.Contains("`") || text.Contains("#") || text.Contains("<");
        }

        private bool ContainsGrammarIssue(string text)
        {
            // Common grammar issues
            return text.Contains("  ") || // double spaces
                   text.Contains(",.") || // misplaced punctuation
                   text.Contains(".,") ||
                   text.Contains("  ,") ||
                   text.Contains("  .") ||
                   text.EndsWith(" ,") ||
                   text.EndsWith(" .");
        }

        private bool IsLikelyGrammarFix(string original, string updated)
        {
            // Check if this is a small word change like a/the, is/was, etc.
            var originalWords = original.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var updatedWords = updated.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            // If word count is same or just 1-2 different
            if (Math.Abs(originalWords.Length - updatedWords.Length) <= 2)
            {
                // Check for common grammar fixes
                var commonGrammarWords = new[] { "a", "an", "the", "is", "are", "was", "were", "has", "have", "had", 
                                                "its", "it's", "your", "you're", "their", "they're", "there" };
                
                // If the difference involves these common grammar words
                var allWords = originalWords.Concat(updatedWords).ToList();
                return allWords.Any(w => commonGrammarWords.Contains(w.ToLower()));
            }
            
            return false;
        }

        private string GenerateProgressMessage(Dictionary<string, List<ContentChunk>> chunksByType)
        {
            StringBuilder message = new StringBuilder("Making improvements to your content:\n\n");
            
            int headings = chunksByType["headings"].Count;
            int paragraphs = chunksByType["paragraphs"].Count;
            int formatting = chunksByType["formatting"].Count;
            int grammar = chunksByType["grammar"].Count;
            int other = chunksByType["other"].Count;
            
            if (headings > 0)
                message.AppendLine($"• Improved {headings} {(headings == 1 ? "heading" : "headings")}");
            
            if (paragraphs > 0)
                message.AppendLine($"• Enhanced {paragraphs} {(paragraphs == 1 ? "paragraph" : "paragraphs")}");
            
            if (formatting > 0)
                message.AppendLine($"• Fixed {formatting} formatting {(formatting == 1 ? "issue" : "issues")}");
            
            if (grammar > 0)
                message.AppendLine($"• Corrected {grammar} grammar/style {(grammar == 1 ? "error" : "errors")}");
            
            if (other > 0)
                message.AppendLine($"• Made {other} other {(other == 1 ? "improvement" : "improvements")}");
            
            message.AppendLine("\nStill analyzing your content...");
            
            return message.ToString();
        }

        private string GenerateCompletionMessage(Dictionary<string, List<ContentChunk>> chunksByType)
        {
            StringBuilder message = new StringBuilder("I've improved your content with the following changes:\n\n");
            
            int headings = chunksByType["headings"].Count;
            int paragraphs = chunksByType["paragraphs"].Count;
            int formatting = chunksByType["formatting"].Count;
            int grammar = chunksByType["grammar"].Count;
            int other = chunksByType["other"].Count;
            
            if (headings > 0)
                message.AppendLine($"✅ Improved {headings} {(headings == 1 ? "heading" : "headings")}");
            
            if (paragraphs > 0)
                message.AppendLine($"✅ Enhanced {paragraphs} {(paragraphs == 1 ? "paragraph" : "paragraphs")}");
            
            if (formatting > 0)
                message.AppendLine($"✅ Fixed {formatting} formatting {(formatting == 1 ? "issue" : "issues")}");
            
            if (grammar > 0)
                message.AppendLine($"✅ Corrected {grammar} grammar/style {(grammar == 1 ? "error" : "errors")}");
            
            if (other > 0)
                message.AppendLine($"✅ Made {other} other {(other == 1 ? "improvement" : "improvements")}");
            
            message.AppendLine("\nAll changes have been applied to your document.");
            
            return message.ToString();
        }

        private async Task SendAssistantMessageUpdateAsync(Guid conversationId, ChatMessageDTO message)
        {
            var notification = new ChatMessageResponseReceived(conversationId, message, null);

            var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            await notifierGrain.NotifyChatMessageResponseReceivedAsync(notification);
        }

        private async Task SendChunkUpdateAsync(Guid conversationId, Guid messageId,
            List<ContentChunk> chunks, bool isComplete)
        {
            var notification = new ContentChunkUpdate
            {
                ConversationId = conversationId,
                MessageId = messageId,
                Chunks = chunks,
                IsComplete = isComplete,
                StatusMessage = isComplete ? "Content update complete." : null
            };

            var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            await notifierGrain.NotifyContentChunkUpdateAsync(notification);
        }

        private async Task SendCompletionNotificationAsync(Guid conversationId, Guid messageId)
        {
            var notification = new ContentChunkUpdate
            {
                ConversationId = conversationId,
                MessageId = messageId,
                Chunks = new List<ContentChunk>(),
                IsComplete = true,
                StatusMessage = "Content update complete."
            };

            var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            await notifierGrain.NotifyContentChunkUpdateAsync(notification);
        }

        private async Task SendStatusUpdateAsync(Guid conversationId, Guid messageId, string message, bool isComplete)
        {
            var notification = new ChatMessageStatusNotification(messageId, message)
            {
                ProcessingComplete = isComplete,
                Persistent = !isComplete
            };

            var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            await notifierGrain.NotifyChatMessageStatusAsync(notification);
        }

        private string CreateChunkProcessingPrompt(string originalContent, string userQuery)
        {
            return $@"
You are improving content based on user instructions. Instead of providing a complete replacement, 
you will identify specific chunks of text to modify and provide those changes in a structured format.

ORIGINAL CONTENT:
{originalContent}

USER REQUEST:
{userQuery}

INSTRUCTIONS:
1. Analyze the content and the user's request.
2. For each change you want to make, output a chunk in this exact format:

<CHUNK>
ORIGINAL: [exact text being replaced]
NEW: [new replacement text]
START_POS: [integer start position in original text]
END_POS: [integer end position in original text]
TYPE: [Replace, Insert, or Delete]
CONTEXT: [text before the change]...[text after the change]
</CHUNK>

3. You can provide multiple chunks for different parts of the content.
4. Be precise with start and end positions - they refer to character indices in the original text.
5. For insertions, ORIGINAL will be empty and START_POS and END_POS will be the same position.
6. For deletions, NEW will be empty.
7. Include enough context to uniquely identify the location - always include 10-20 characters before and after the change.
8. Do not output the entire modified document - only output the chunks.
9. IMPORTANT: Keep chunks small and focused - don't try to replace huge sections at once.
10. IMPORTANT: If simple word or phrase replacements are needed, break them into individual chunks.

Please identify the specific changes needed to improve this content according to the user's request.
Respond with only chunk definitions, no explanatory text before or after the chunks.";
        }

        private List<ContentChunk> ExtractContentChunks(string aiResponse, string originalContent)
        {
            var chunks = new List<ContentChunk>();

            // Updated regex pattern to correctly match serialized chunk definitions
            var chunkPattern = @"<CHUNK>\s*ORIGINAL:\s*(.*?)\s*NEW:\s*(.*?)\s*START_POS:\s*(\d+)\s*END_POS:\s*(\d+)\s*TYPE:\s*(\w+)\s*CONTEXT:\s*(.*?)\s*</CHUNK>";
            var matches = Regex.Matches(aiResponse, chunkPattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 7)
                {
                    var originalText = match.Groups[1].Value.Trim();
                    var newText = match.Groups[2].Value.Trim();

                    // Parse positions, defaulting to 0 if parsing fails
                    if (!int.TryParse(match.Groups[3].Value, out int startPos))
                        startPos = 0;

                    if (!int.TryParse(match.Groups[4].Value, out int endPos))
                        endPos = 0;

                    // Parse chunk type
                    if (!Enum.TryParse<ContentChunkType>(match.Groups[5].Value, true, out var chunkType))
                        chunkType = ContentChunkType.Replace;

                    var context = match.Groups[6].Value.Trim();

                    // Add position validation and correction
                    bool isValid = ValidateAndCorrectPositions(
                        ref startPos, ref endPos, ref originalText, chunkType, originalContent);

                    if (isValid)
                    {
                        chunks.Add(new ContentChunk
                        {
                            OriginalText = originalText,
                            NewText = newText,
                            StartPosition = startPos,
                            EndPosition = endPos,
                            ChunkType = chunkType,
                            Context = context
                        });
                    }
                }
            }

            return chunks;
        }

        private bool ValidateAndCorrectPositions(
            ref int startPos, ref int endPos, ref string originalText,
            ContentChunkType chunkType, string originalContent)
        {
            // For insertions, just make sure positions are within bounds
            if (chunkType == ContentChunkType.Insert)
            {
                if (startPos < 0 || startPos > originalContent.Length)
                    return false;

                // For insert, ensure start and end are the same
                endPos = startPos;
                return true;
            }

            // For replacement and deletion operations
            if (startPos < 0 || endPos > originalContent.Length || startPos >= endPos)
            {
                // Try to fix the positions by finding the original text
                if (!string.IsNullOrEmpty(originalText))
                {
                    var index = originalContent.IndexOf(originalText);
                    if (index >= 0)
                    {
                        startPos = index;
                        endPos = index + originalText.Length;
                        return true;
                    }
                }
                return false;
            }

            // Validate that what's at the positions matches the claimed original text
            var actualText = originalContent.Substring(startPos, endPos - startPos);
            if (actualText != originalText)
            {
                // Try to find the correct position
                var index = originalContent.IndexOf(originalText);
                if (index >= 0)
                {
                    startPos = index;
                    endPos = index + originalText.Length;
                    return true;
                }
                return false;
            }

            return true;
        }

        private string CreateChunkRemovalPattern(ContentChunk chunk)
        {
            // Create a pattern that matches the chunk definition in the AI response
            return $@"<CHUNK>\s*ORIGINAL:\s*{Regex.Escape(chunk.OriginalText)}\s*NEW:\s*{Regex.Escape(chunk.NewText)}\s*START_POS:\s*{chunk.StartPosition}\s*END_POS:\s*{chunk.EndPosition}\s*TYPE:\s*{chunk.ChunkType}\s*CONTEXT:\s*{Regex.Escape(chunk.Context)}\s*</CHUNK>";
        }

        // Generate a unique signature for each chunk to detect duplicates
        private string GenerateChunkSignature(ContentChunk chunk)
        {
            return $"{chunk.ChunkType}_{chunk.StartPosition}_{chunk.EndPosition}_{chunk.OriginalText?.GetHashCode() ?? 0}_{chunk.NewText?.GetHashCode() ?? 0}";
        }
    }
}
