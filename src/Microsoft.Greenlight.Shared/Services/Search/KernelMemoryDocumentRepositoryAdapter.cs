// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Adapter that wraps the existing <see cref="IKernelMemoryRepository"/> implementation to expose the
/// generic document repository abstraction used for either Kernel Memory or Semantic Kernel Vector Store.
/// </summary>
/// <remarks>
/// This adapter is retained only for backward compatibility with legacy (Classic / Kernel Memory) document
/// processes that still request an <see cref="IDocumentRepository"/> but internally rely on KM-specific
/// return types. New code should depend directly on <see cref="IDocumentRepository"/> implementations
/// (e.g. <see cref="SemanticKernelVectorStoreRepository"/>) without introducing additional adapters.
/// A future cleanup may remove this type once all legacy processes are migrated.
/// </remarks>
[Obsolete("Legacy bridge for Kernel Memory document processes. Avoid new dependencies and migrate away when possible.")]
public class KernelMemoryDocumentRepositoryAdapter : IDocumentRepository
{
	private readonly IKernelMemoryRepository _inner;

	/// <summary>
	/// Creates a new instance of the <see cref="KernelMemoryDocumentRepositoryAdapter"/> class.
	/// </summary>
	/// <param name="inner">Underlying KM repository.</param>
	public KernelMemoryDocumentRepositoryAdapter(IKernelMemoryRepository inner)
	{
		_inner = inner;
	}

	/// <inheritdoc />
	public Task StoreContentAsync(string documentLibraryName, string indexName, Stream fileStream, string fileName, string? documentUrl, string? userId = null, Dictionary<string, string>? additionalTags = null)
		=> _inner.StoreContentAsync(documentLibraryName, indexName, fileStream, fileName, documentUrl, userId, additionalTags);

	/// <inheritdoc />
	public Task DeleteContentAsync(string documentLibraryName, string indexName, string fileName)
		=> _inner.DeleteContentAsync(documentLibraryName, indexName, fileName);

	/// <inheritdoc />
	public async Task<List<SourceReferenceItem>> SearchAsync(string documentLibraryName, string searchText, ConsolidatedSearchOptions options)
	{
		var kmResults = await _inner.SearchAsync(documentLibraryName, searchText, options);
		// KM results already derive from SourceReferenceItem hierarchy, so we can safely cast.
		return kmResults.Cast<SourceReferenceItem>().ToList();
	}

	/// <inheritdoc />
	public async Task<DocumentRepositoryAnswer?> AskAsync(string documentLibraryName, string indexName, Dictionary<string, string>? parametersExactMatch, string question)
	{
		var answer = await _inner.AskAsync(documentLibraryName, indexName, parametersExactMatch, question);
		if (answer == null)
		{
			return null;
		}

		// Convert KM MemoryAnswer + citations to generic answer model
		// KM MemoryAnswer doesn't expose a single relevance score; approximate by highest partition relevance.
		var highestPartitionScore = 0.0;
		foreach (var c in answer.RelevantSources)
		{
			foreach (var p in c.Partitions)
			{
				if (p.Relevance > highestPartitionScore) highestPartitionScore = p.Relevance;
			}
		}

		var generic = new DocumentRepositoryAnswer
		{
			Result = answer.Result,
			Relevance = highestPartitionScore,
			RelevantSources = []
		};

		foreach (var citation in answer.RelevantSources)
		{
			var docCitation = new DocumentCitation
			{
				Link = citation.Partitions.FirstOrDefault()?.Tags.TryGetValue("OriginalDocumentUrl", out var links) == true ? links.FirstOrDefault() ?? string.Empty : string.Empty,
				Index = citation.Index,
				DocumentId = citation.DocumentId,
				FileId = citation.FileId
			};

			foreach (var part in citation.Partitions)
			{
				var chunk = new DocumentChunk
				{
					Text = part.Text ?? string.Empty,
					Relevance = part.Relevance,
					PartitionNumber = part.PartitionNumber,
					SizeInBytes = part.Text?.Length ?? 0,
					Tags = part.Tags?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, List<string?>>(),
					LastUpdate = part.LastUpdate
				};
				docCitation.Partitions.Add(chunk);
			}
			generic.RelevantSources.Add(docCitation);
		}

		return generic;
	}
}

