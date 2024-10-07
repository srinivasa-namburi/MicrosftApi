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
        The 5 last chat messages are between the [ChatHistory] and [/ChatHistory] tags.
        A summary of full conversation history is between the [ChatHistorySummary] and [/ChatHistorySummary] tags.
        The user's question is between the [User] and [/User] tags.

        Consider this chat history when responding to the user, specifically 
        looking for any context that may be relevant to the user's question.

        Be precise when answering the user's query, don't provide unnecessary information 
        that goes beyond the user's question.

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
        This is the initial query in a multi-pass conversation. You are not expected to return the full output in this pass.
        However, please be as complete as possible in your response for this pass. For this task, including this initial query,
        we will be performing {{ numberOfPasses }} passes to form a complete response. This is the first pass.

        There will be additional queries asking to to expand on a combined summary of the output you provide here and
        further summaries from later responses.

        You are writing the section {{ fullSectionName }}. The section examples may contain input from
        additional sub-sections in addition to the specific section you are writing.

        Below, there are several extracts/fragments from previous applications denoted by [EXAMPLE: Document Extract].

        Using this information, write a similar section(sub-section) or chapter(section),
        depending on which is most appropriate to the query. The fragments might contain information from different sections, 
        not just the one you are writing ({{ fullSectionName }}). Filter out any irrelevant information and write a coherent section.

        For customizing the output so that it pertains to this project, please use tool calling/functions as supplied to you
        in the list of available functions. If you need additional data, please request it using the [DETAIL: <dataType>] tag.

        Use the name of the area instead of referring to it by lat/long in the text. If you can't find a suitable name for the area, you can use the lat/long.
        Whenever you're using the lat/long in the text, use the native_FacilitiesPlugin to generate an image of the map and attach the relative url path in a markdown manner as an image. Do not attempt to translate it.
        
        In particular, pay attention to paragraphs that refer to the geographical area of the source documents, which is likely
        to be different from the area of the project you are writing about. Make sure to adapt the content to the project area. Use the plugins
        available to you as well as your general knowledge to replace information about roads, rivers, lakes, and other geographical features
        with similar features and information about the project area. 

        Use the native_FacilitiesPlugin (if available) to look for geographical markers.

        Use the native_EarthQuakePlugin if you need to find seismic history for an area.

        If you do use plugins, please denote them as references at the end of the section you are writing.
        For the native_FacilitiesPlugin, write the source as Azure Maps (API).
        For the native_EarthQuakePlugin, write the source as US Geological Survey (API).

        ONLY supply these plugin references if you use the plugins AND their resulting output. If you don't use the output directly or just
        to supply a geographical area name, please don't include them as references.

        Custom data for this project follows in JSON format between the [CUSTOMDATA] and [/CUSTOMDATA] tags. IGNORE the following fields:
        DocumentProcessName, MetadataModelName, DocumentGenerationRequestFullTypeName, ID, AuthorOid.

        [CUSTOMDATA]
        {{ customDataString }}
        [/CUSTOMDATA]

        In between the [TOC] and [/TOC] tags below, you will find a table of contents for the entire document.
        Please make sure to use this table of contents to ensure that the section you are writing fits in with the rest of the document,
        and to avoid duplicating content that is already present in the document. Pay particular attention to neighboring sections and the
        parent title of the section you're writing. If you see references to sections in the section you're writing,
        please use this TOC to validate chapter and section numbers and to ensure that the references are correct. Please don't
        refer to tables or sections that are not in the TOC provided here.

        [TOC]
        {{ tableOfContentsString }}
        [/TOC]

        Be as verbose as necessary to include all required information. Try to be very complete in your response, considering all source data. 
        We are looking for full sections or chapters - not short summaries.

        For headings, remove the numbering and any heading prefixes like "Section" or "Chapter" from the heading text.

        Make sure your output complies with UTF-8 encoding. Paragraphs should be separated by two newlines (\n\n)
        For lists, use only * and - characters as bullet points, and make sure to have a space after the bullet point character.

        Format the text with Markdown syntax. For example, use #, ##, ### for headings, * and - for bullet points, etc.

        For table outputs, look at the example content and look for tables that match the table description. Please render them inline or at the end
        of the text as Markdown Tables. If they are too complex, you can use HTML tables instead that accomodate things like rowspan and colspan.
        You should adopt the contents of these tables to fit any custom inputs you have received regarding location, size,
        and so on. If you have no such inputs, consider the tables as they are in the examples as the default.

        If you are missing details to write specific portions of the text, please indicate that with [DETAIL: <dataType>] -
        and put the type of data needed in the dataType parameter. Make sure to look at all the source content before you decide you lack details!

        Be concise in these DETAIL requests - no long sentences, only data types with a one or two word description of what's missing.

        If you believe the output is complete (and this is the last needed pass to complete the whole section), please end your response with the following text on a
        new line by itself:
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
         This is a continuing conversation.
         
         You're now going to continue the previous conversation, expanding on the previous output.
         A summary of the output up to now is here :
         {{ summary }}
         
         As a reminder, this was the output of the last pass - DO NOT REPEAT THIS INFORMATION IN THIS PASS:
         {{ lastPassResponseJoinedByDoubleLineFeeds }}
         
         For the next step, you should continue the conversation. Here's the prompt you should use - but
         take care not to repeat the same information you've already provided (as detailed in the summary). Also ignore the initial 
         heading and first two paragraphs of the prompt detailing how to respond to the first pass query. 
         This is pass number {{ passNumber }} of {{ numberOfPasses }} - take that into account when you respond.
         
         Please start your response with content only - no ASSISTANT texts explaining your logic, tasks or reasoning.
         The output from the passes should be possible to tie together with no further parsing necessary.
         
         If you believe the output for the whole section is complete (and this is the last needed pass),
         please end your response with the following text on a
         new line by itself:
         [*COMPLETE*]
         
         Note - do NOT use that tag to delineate the end of a single response. It should only be used to indicate the end of the whole section
         when no more passes are needed to finish the section output.
         
         ORIGINAL PROMPT with examples:
         {{ originalPrompt }}
        """;
}
