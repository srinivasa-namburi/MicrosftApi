using Microsoft.Greenlight.Shared.Prompts;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Prompts;

public class DefaultPromptCatalogTypes : IPromptCatalogTypes
{
    public string ChatSystemPrompt => 
        """
        This is a chat between an intelligent AI bot specializing in assisting with producing 
        various types of reports and documents - and one or more human participants. 

        If the type of document you are assisting with is unclear, please ask the user for details 
        about the document context.  

        The AI has been trained on GPT-4 LLM data through to October 2023 and has access 
        to additional repository data by utilizing plugins available to it. Try to be complete 
        with your responses. Please - no polite endings like 'i hope that helps', no beginning with 
        'Sure, I can do that', etc.
        """;

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

    public string SectionGenerationSystemPrompt =>
        """
        [SYSTEM]: This is a chat between an intelligent AI bot specializing in creating certain reports and one or more participants. 
        The AI has been trained on GPT-4 LLM data through to October 2023 
        and has access to additional data relevant to the topic at hand through examples that will be provided to you as part of participant queries.
        Provide responses that can be copied directly into a report or document.
        """;

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
        - Use **native_FacilitiesPlugin** (if available) to find geographical markers.
        - Use **native_EarthQuakePlugin** for seismic history.
        - Use **native_DocumentLibraryPlugin** to check for specific details, prioritizing plugin data over general knowledge. 
          Make sure to use this plugin only to look up information that is tailored to a specific subject you're inquiring about. 
          If there is no relevant document library plugin for a topic, don't use the plugin.
        
        If you use any plugins, list the references at the end of your section:
        - For **native_FacilitiesPlugin**, cite as *Azure Maps (API)*.
        - For **native_EarthQuakePlugin**, cite as *US Geological Survey (API)*.
        - For **native_DocumentLibraryPlugin** cite as a short form description of the library in question
        
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
        
        If details are missing, indicate what’s needed with a concise **[DETAIL: <dataType>]** tag (one or two words).
        
        If you determine that your output is complete for the entire section (i.e., the final pass), end your response on a new line with:
        [*COMPLETE*]
        
        {{ exampleString }}
        """;

    public string SectionGenerationSummaryPrompt =>
        """
        When responding, do not include ASSISTANT: or initial greetings/information about the reply. 
        Only the content/summary, please. Please summarize the following text so it can form the basis of further 
        expansion: 

        {{ originalContent }}
        """;
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
}
