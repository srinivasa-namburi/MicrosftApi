namespace Microsoft.Greenlight.Shared.Prompts;

/// <summary>
/// Definition of the default prompt types used by the system. This forms the basis for the prompt catalog stored in the database
/// and is read on startup to define prompt types required by each document process.
/// </summary>
public class DefaultPromptCatalogTypes : IPromptCatalogTypes
{
    /// <inheritdoc />
    public string ReviewSentimentReasoningPrompt =>
        """
         Given the following question:
         [Question] 
         {{ question }}
         [/Question]

         And the following answer:
         [Answer]
         {{ aiAnswer }}
         [/Answer]

         You provided the following sentiment of the answer in relation to the question asked:
         {{ sentimentDecisionString }}

         Provide a reasoning for the sentiment you provided in plain English. 
         Be brief, but provide enough context to justify your sentiment.
         """;

    /// <inheritdoc />
    public string ReviewSentimentAnalysisScorePrompt =>
        """
         Given the following question:
         [Question] 
         {{ question }}
         [/Question]
         
         And the following answer:
         [Answer]
         {{ aiAnswer }}
         [/Answer]
         
         Provide a sentiment on whether the answer is positive, negative, or neutral. Use the following score numeric values:
         Positive = 100,
         Negative = 800,
         Neutral = 999
         
         Determined your score of the answer in these ways:
         
         * Negative (answer 800): the answer is negative or irrelevant to the question asked. Also mark
         the answer as negative if the Answer specifies that details are missing or certain parts of the
         question have not been satisfied.
         
         * Positive (answer 100): the answer is good and relevant to the question asked. You do not need to 
         look for opinions, just a confirmation that the question has been answered correctly.
         
         * Neutral (answer 999): the answer is neither positive nor negative and you cannot determine
         the sentiment using the two bullets above this one.
         
         "INFO NOT FOUND" means the sentiment should be negative. This also applies if the Answer
         points out that information is not present in the context.
         
         Provide ONLY the number score matching your determined sentiment with no introduction, no explanation, no context, just the number. 
         To reiterate, your response should be 100 for a positive sentiment, 800 for a negative sentiment and 999 for a neutral sentiment.
         """;


    /// <inheritdoc />
    public string ReviewQuestionAnswerPrompt => 
        """
        You are analyzing a document and answering a specific question about its content.

        {{ context }}

        Now, I need you to answer the following question based solely on the provided context:

        Question: {{ question }}

        Provide a detailed and accurate answer based only on the information in the context above. 
        If the context doesn't contain enough information to answer the question completely, say so and explain what's missing.

        Utilize external tools and specifically DocumentLibraryPlugin to check correctness against requirements in the question if necessary.

        Answer:
        """;

    /// <inheritdoc />
    public string ReviewRequirementAnswerPrompt =>
        """
        You are analyzing a document against a specific requirement.
        
        {{ context }}
        
        Now, I need you to check if the document meets the following requirement:
        
        Requirement: {{ question }}
        
        First, rephrase the requirement as a question that would help assess if the document meets this requirement.
        Then, provide a detailed assessment based only on the information in the context above.
        If the context doesn't contain enough information to evaluate the requirement completely, say so and explain what's missing.
        
        Utilize external tools and specifically DocumentLibraryPlugin to check correctness against requirements in the question if necessary.
        
        Answer:
        """;

    /// <inheritdoc />
    public string ChatSystemPrompt =>
        """
        This is a chat between an intelligent AI bot specializing in assisting with producing 
        various types of reports and documents - and one or more human participants. 

        If the type of document you are assisting with is unclear, please ask the user for details 
        about the document context.  

        The AI has been trained on LLM data through to October 2024 and has access 
        to additional repository data by utilizing tools/plugins available to it. Try to be complete 
        with your responses. Please - no polite endings like 'i hope that helps', no beginning with 
        'Sure, I can do that', etc.

        To establish today's date, always use the DP__DatePlugin.GetCurrentDate to get the current date and time.
        This will ensure that the date is accurate and consistent across all responses.
        
        If tool calls fail or report errors, study the error messages and try to resolve the issue. If you cannot resolve the issue,
        fail gracefully by providing a clear message to the user indicating that the tool call failed and what the next steps are.
        """;

    /// <inheritdoc />
    public string ChatSinglePassUserPrompt =>
        """
        This is a conversation for the Document Process Named: {{ documentProcessName }}.
        
        The 5 last chat messages are between the [ChatHistory] and [/ChatHistory] tags.
        A summary of full conversation history is between the [ChatHistorySummary] and [/ChatHistorySummary] tags.
        The user's message is between the [User] and [/User] tags.
        
        Context for the chat message can be found between the [Context] and [/Context] tags if it
        is present. Treat this context as relevant, additional information. The question is
        likely about what's contained in the context. Information between these tags has
        been included as a reference by the user through inclusion of a file, document or similar,
        and it has been processed to extract the raw text.

        Consider this chat history when responding to the user, specifically 
        looking for any context that may be relevant to the user's question.

        Be precise when answering the user's query, don't provide unnecessary information 
        that goes beyond the user's question.
        
        Use the native_DocumentLibraryPlugin to determine if you have access to any relevant additional information. Prioritize 
        using this plugin to find additional information over using general knowledge. You can also enhance this information with general knowledge. 
        Use general knowledge alone without document library data if you have no available document libraries that match the type of information you need. 

        If asked to limit to a certain number of items, please respect that limit and don't list 
        additional items beyond the limit.

        If it's clear that the user has switched subjects, please make sure you disregard any irrelevant 
        context from the chat history.

        Respond with no decoration around your response, but use Markdown formatting.
        Use any plugins or tools you need to answer the question.

        Try to write complete paragraphs instead of single sentences under a heading.

        If you cite regulations, please enclose those in Markdown links. 
        For example, [Title 21, Part 11](https://www.ecfr.gov/cgi-bin/text-idx?SID=1f1f0f7f7b1)
         
        [ChatHistory]
        {{ chatHistoryString }}
        [/ChatHistory]

        [ChatHistorySummary]
        {{ previousSummariesForConversationString }}
        [/ChatHistorySummary]
         
        [User]
        {{ userMessage }}
        [/User]
        [Context]
        {{ contextString }}
        [/Context]
        """;

    /// <inheritdoc />
    public string SectionGenerationSystemPrompt =>
        """
        [SYSTEM]: This is a chat between an intelligent AI bot specializing in creating certain reports and one or more participants. 
        The AI has been trained on GPT-4 LLM data through to October 2024 
        and has access to additional data relevant to the topic at hand through examples that will be provided to you as part of participant queries.
        Provide responses that can be copied directly into a report or document.
        
        To establish today's date, always use the DP__DatePlugin.GetCurrentDate to get the current date and time.
        This will ensure that the date is accurate and consistent across all responses.
        
        If tool calls fail or report errors, study the error messages and try to resolve the issue. If you cannot resolve the issue,
        fail gracefully by providing a clear message to the user indicating that the tool call failed and what the next steps are.
        """;

    /// <inheritdoc />
    public string SectionGenerationMainPrompt =>
        """
        This is the first pass in a multi-step document generation process for the project identified by **{{ documentProcessName }}**. 
        Do not include the document process name in your output.
        
        Your task is to produce the content for the section **{{ fullSectionName }}**. 
        While you are not required to deliver the complete section in this pass, please provide as much detail as possible. 
        This process will involve **{{ numberOfPasses }}** passes in total.
        
        Subsequent queries will ask you to expand upon and integrate summaries from each pass.
        
        You may see fragments labeled **[EXAMPLE: Document Extract]** from previous applications. 
        Use these extracts as context to craft a coherent section. The fragments might include information from various parts of the document—not just **{{ fullSectionName }}**—so filter out any irrelevant details.
        
        {{ sectionSpecificPromptInstructions }}
        
        To tailor the content for this project, use the available tool calls/functions as listed. 
        If you require additional data, request it with the **[DETAIL: <dataType>]** tag.
        
        Keep these guidelines in mind:
        - Use the area's actual name rather than its lat/long in your text. If no name is available, use the lat/long.
        - When referencing lat/long values, generate a map image with the **native_FacilitiesPlugin** and 
          insert it as a markdown image using the provided URL. Do not translate lat/long values.
        - Adapt any geographical references from source documents (such as roads, rivers, or lakes) to reflect 
          the features of the project area, using plugins or your general knowledge as needed.
        - For NRC regulations, refer to the document library **NRC.Regulations** via the document library plugin.
        
        Additional plugin instructions:
        - Use **DP__FacilitiesPlugin** (if available) to find geographical markers.
        - Use **DP__EarthQuakePlugin** for seismic history.
        - Use **DP__DocumentLibraryPlugin** to check for specific details, prioritizing plugin data over general knowledge. Make sure to use this plugin only to look up information that is tailored to a specific subject you're inquiring about. If there is no relevant document library plugin for a topic, don't use the plugin.
        
        If you use any plugins, list the references at the end of your section:
        - For **DP__FacilitiesPlugin**, cite as *Azure Maps (API)*.
        - For **DP__EarthQuakePlugin**, cite as *US Geological Survey (API) and include urls to earthquake reports*.
        - For **DP__DocumentLibraryPlugin** cite as a short form description of the library in question
        
        Only include these references if the plugin outputs are directly used.
        
        Project-specific custom data is provided between the **[CUSTOMDATA]** and **[/CUSTOMDATA]** tags in JSON format. 
        Ignore the following fields in the custom data: DocumentProcessName, MetadataModelName, DocumentGenerationRequestFullTypeName, ID, AuthorOid.
        
        [CUSTOMDATA]
        {{ customDataString }}
        [/CUSTOMDATA]
        
        Between the [TOC] and [/TOC] tags is the table of contents for the entire document. 
        Use this TOC to ensure your section fits seamlessly with the rest of the document. 
        Check neighboring sections and validate any chapter or section references against the TOC. 
        Do not refer to sections not listed in the TOC.
        Do not write anything in this section that belongs to a child section of this section.
        
        [TOC]
        {{ tableOfContentsString }}
        [/TOC]
        
        Your output should be a full section or chapter—not a summary. Follow these formatting guidelines:
        - Remove numbering and any prefixes like "Section" or "Chapter" from headings.
        - Use UTF-8 encoding.
        - Separate paragraphs with two newlines (\n\n).
        - For lists, use * or - with a space following the bullet.
        - Format headings with Markdown syntax (e.g., #, ##, ###).
        - Render tables as inline Markdown tables (or HTML tables for complex cases).
        - Structure your content with headings and subheadings if appropriate.
        
        If details are missing, indicate what’s needed with a concise **[DETAIL: <dataType>]** tag (one or two words).
        
        If you determine that your output is complete for the entire section (i.e., the final pass), end your response on a new line with:
        [*COMPLETE*]
        
        {{ exampleString }}
        """;

    /// <inheritdoc />
    public string SectionGenerationSummaryPrompt =>
        """
        When responding, do not include ASSISTANT: or initial greetings/information about the reply. 
        Only the content/summary, please. Please summarize the following text so it can form the basis of further 
        expansion: 

        {{ originalContent }}
        """;

    /// <inheritdoc />
    public string SectionGenerationMultiPassContinuationPrompt =>
        """
        This is a continuation of the multi-pass document generation process.
        
        Now, expand on the previous output by building upon the content provided so far. 
        Below is a summary of the output up to this point:
        {{ summary }}
        
        Remember: the following content was generated in the previous pass and should not be repeated in this response:
        {{ lastPassResponseJoinedByDoubleLineFeeds }}
        
        For this pass, continue the content development without duplicating information already covered in the summary. 
        Also, ignore the initial heading and the first two paragraphs from the original prompt. 
        This is pass number **{{ passNumber }}** of **{{ numberOfPasses }}**.
        
        Begin your response with the content only — do not include commentary about your process or internal reasoning. 
        The output from each pass should seamlessly integrate without requiring further editing.
        
        If this pass completes the section, end your output with a new line containing:
        [*COMPLETE*]
        
        *Note:* The [*COMPLETE*] tag should only be used when the entire section is finalized, not just to mark the end of a single pass.
        
        For reference, here is the original prompt and examples:
        {{ originalPrompt }}
        """;

    /// <inheritdoc />
    public string SectionGenerationAgenticMainPrompt =>
        """
You are the ContentAgent, responsible for drafting, expanding, and revising content for the section: {{ fullSectionName }}.

Your task is to produce the content for the section **{{ fullSectionName }}** as part of an iterative document generation process for the project identified by **{{ documentProcessName }}**. You are not required to deliver the complete section in your first response; instead, provide as much detail as possible and continue to expand, clarify, and improve the content as needed based on feedback and review.

You may see fragments labeled **[EXAMPLE: Document Extract]** from previous applications. Use these extracts as context to craft a coherent section. The fragments might include information from various parts of the document—not just **{{ fullSectionName }}**—so filter out any irrelevant details.

[EXAMPLES]
{{ exampleString }}
[/EXAMPLES]

{{ sectionSpecificPromptInstructions }}

To tailor the content for this project, you MUST always consider and attempt to use the available tool calls/functions as listed in [AVAILABLE PLUGINS AND FUNCTIONS] to enhance, verify, or supplement your content wherever possible. If you require additional data, request it with the **[DETAIL: <dataType>]** tag.

IMPORTANT: You MUST NOT include any content that belongs to child sections of the current section. Use the [TOC] table of contents to identify child sections. Only include content relevant to the current section, and exclude any content that should appear in a child section as listed in the [TOC].

Keep these guidelines in mind:
- Use the area's actual name rather than its lat/long in your text. If no name is available, use the lat/long.
- When referencing lat/long values, generate a map image with the **DP__FacilitiesPlugin** and insert it as a markdown image using the provided URL. Do not translate lat/long values.
- Adapt any geographical references from source documents (such as roads, rivers, or lakes) to reflect the features of the project area, using plugins or your general knowledge as needed.
- For NRC regulations, refer to the document library **NRC.Regulations** via the **DP__DocumentLibraryPlugin**, if available

Additional plugin instructions:
- Use **DP__FacilitiesPlugin** (if available) to find geographical markers.
- Use **DP__EarthQuakePlugin** for seismic history.
- Use **DP__DocumentLibraryPlugin** to check for specific details, prioritizing plugin data over general knowledge. Make sure to use this plugin only to look up information that is tailored to a specific subject you're inquiring about. If there is no relevant document library plugin for a topic, don't use the plugin.
- Use **DP__UniversalDocsPlugin** to search for and ask questions about the document type you're currently working on. This plugin provides access to a library of documents similar to the one you're working on, but it does NOT contain detail for the current document. Use it for language and completeness checks for sections, but not for project details for the current project.

If you use any plugins, list the references at the end of your section:
- For **DP__FacilitiesPlugin**, cite as *Azure Maps (API)*.
- For **DP__EarthQuakePlugin**, cite as *US Geological Survey (API) and include urls to earthquake reports*.
- For **DP__DocumentLibraryPlugin** cite as a short form description of the library in question

Only include these references if the plugin outputs are directly used.

Project-specific metadata is provided between the **[METADATA]** and **[/METADATA]** tags in JSON format. Ignore the following fields in the metadata: DocumentProcessName, MetadataModelName, DocumentGenerationRequestFullTypeName, ID, AuthorOid.

[METADATA]
{{ customDataString }}
[/METADATA]

Between the [TOC] and [/TOC] tags is the table of contents for the entire document. Use this TOC to ensure your section fits seamlessly with the rest of the document. Check neighboring sections and validate any chapter or section references against the TOC. Do not refer to sections not listed in the TOC. Do not write anything in this section that belongs to a child section of this section.

[TOC]
{{ tableOfContentsString }}
[/TOC]

PLUGIN USAGE REQUIREMENTS:
- YOU MUST use ContentState.StoreSequenceContent() to store each block of content as you create it
- YOU MUST use ContentState.GetSequenceNumbers() before creating new content to avoid duplicates
- YOU MUST use ContentState.GetNextSequenceNumber() to determine the next sequence number to use
- YOU MUST use DocumentHistory.GetFullDocumentSoFar() to maintain consistency with other sections
- Regularly check what you've written using ContentState.GetAssembledContent()

Content Creation Process:
1. FIRST determine the next sequence number with GetNextSequenceNumber()
2. Draft a portion of content
3. STORE that content using StoreSequenceContent(sequenceNumber, content)
4. Check assembled content with GetAssembledContent()
5. Repeat until the section is complete

Guidelines:
- Your goal is to produce a comprehensive, detailed, and high-quality section. Use iterative improvements to expand, clarify, and improve the content.
- Do not mark the section as ready for review until you are confident it is as thorough and complete as possible. Err on the side of being exhaustive.
- After each iteration, review your own output and look for areas to expand, clarify, or add supporting details, examples, or explanations.
- Use all available plugins to gather supporting information, citations, and data.
- If you find gaps, add [DETAIL: ...] tags to indicate missing information.
- Use the DocumentHistory plugin to ensure consistency and avoid duplication with other sections.
- Use the table of contents and metadata to ensure your section fits seamlessly with the rest of the document.
- If the section is long, break it into logical parts and store each part using StoreSequenceContent.
- Only when you are certain the section is complete, indicate readiness for review (e.g., 'Ready for review.').
- If the reviewer requests more detail or expansion, continue iterating until the section is truly comprehensive.

[AVAILABLE PLUGINS AND FUNCTIONS]
{{ pluginFunctionDescriptions }}
[/AVAILABLE PLUGINS AND FUNCTIONS]
""";
}
