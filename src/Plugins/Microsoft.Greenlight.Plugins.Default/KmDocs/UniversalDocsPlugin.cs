// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Microsoft.Greenlight.Plugins.Default.KmDocs;

/// <summary>
/// Enhanced document plugin that supports both Kernel Memory and Semantic Kernel Vector Store.
/// </summary>
public class UniversalDocsPlugin : IPluginImplementation
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DocumentProcessInfo _documentProcess;
    private readonly ConsolidatedSearchOptions _searchOptions;

    /// <summary>
    /// This class needs to be constructed per Document Process, which is handled in the paired PluginRegistration class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory to resolve per-call services safely.</param>
    /// <param name="documentProcess">The current document process info.</param>
    /// <param name="searchOptions">Default search options for this process.</param>
    public UniversalDocsPlugin(IServiceScopeFactory scopeFactory, DocumentProcessInfo documentProcess, ConsolidatedSearchOptions searchOptions)
    {
        _scopeFactory = scopeFactory;
        _documentProcess = documentProcess;
        _searchOptions = searchOptions;
    }

    [KernelFunction(nameof(AskQuestionAsync))]
    [Description("Ask a question to the underlying library of documents similar to the one you're working on. This knowledge base provides chunks of other documents of the same type, but they do NOT contain detail for the current document. Use for language and completeness checks for sections, but not for project details for the current project.")]
    public async Task<string> AskQuestionAsync(
        [Description("The question to ask the document repository. Make sure to format as a proper question ending with a question mark")]
        string question)
    {
        var index = _documentProcess.Repositories[0];

        using var scope = _scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDocumentRepositoryFactory>();
        var repository = await factory.CreateForDocumentProcessAsync(_documentProcess);

        var answer = await repository.AskAsync(_documentProcess.ShortName, index, null, question);
        return answer?.Result ?? "I'm sorry, I don't have an answer for that.";
    }

    [KernelFunction(nameof(SearchKnowledgeBase))]
    [Description("Search the library of documents similar to the one you're working on. Prefer this over AskQuestionAsync. This knowledge base provides chunks of other documents of the same type, but they do NOT contain detail for the current document. Use for language and completeness checks for sections, but not for project details for the current project.")]
    public async Task<List<SourceReferenceItem>> SearchKnowledgeBase(
        [Description("The search query to use")]
        string searchQuery)
    {
        using var scope = _scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDocumentRepositoryFactory>();
        var repository = await factory.CreateForDocumentProcessAsync(_documentProcess);

        var searchResults = await repository.SearchAsync(_documentProcess.ShortName, searchQuery, _searchOptions);

        // Return the generic SourceReferenceItem list directly
        return searchResults;
    }
}