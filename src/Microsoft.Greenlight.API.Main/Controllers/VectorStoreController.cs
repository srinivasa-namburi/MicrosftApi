// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Vector store queries for document libraries and document processes.
/// Simplified controller that distinguishes scope via <see cref="DocumentLibraryType"/>.
/// </summary>
[ApiController]
[Route("api/vector-store")]
public sealed class VectorStoreController : ControllerBase
{
    private readonly ILogger<VectorStoreController> _logger;
    private readonly IDocumentRepositoryFactory _repositoryFactory;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IConsolidatedSearchOptionsFactory _optionsFactory;
    private readonly IMapper _mapper;

    public VectorStoreController(
        ILogger<VectorStoreController> logger,
        IDocumentRepositoryFactory repositoryFactory,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IDocumentProcessInfoService documentProcessInfoService,
        IConsolidatedSearchOptionsFactory optionsFactory,
        IMapper mapper)
    {
        _logger = logger;
        _repositoryFactory = repositoryFactory;
        _documentLibraryInfoService = documentLibraryInfoService;
        _documentProcessInfoService = documentProcessInfoService;
        _optionsFactory = optionsFactory;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets the effective saved search options for the specified scope.
    /// </summary>
    /// <param name="type">Scope type: PrimaryDocumentProcessLibrary or AdditionalDocumentLibrary.</param>
    /// <param name="shortName">Process short name or library short name.</param>
    [HttpGet("options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<ConsolidatedSearchOptions>> GetOptions([FromQuery] DocumentLibraryType type, [FromQuery] string shortName)
    {
        if (string.IsNullOrWhiteSpace(shortName))
        {
            return BadRequest("shortName is required");
        }

        switch (type)
        {
            case DocumentLibraryType.PrimaryDocumentProcessLibrary:
            {
                var process = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(shortName);
                if (process == null) return NotFound();
                var options = await _optionsFactory.CreateSearchOptionsForDocumentProcessAsync(process);
                return Ok(options);
            }
            case DocumentLibraryType.AdditionalDocumentLibrary:
            {
                var library = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(shortName);
                if (library == null) return NotFound();
                var options = await _optionsFactory.CreateSearchOptionsForDocumentLibraryAsync(library);
                return Ok(options);
            }
            default:
                return BadRequest("Unsupported scope type for vector search.");
        }
    }

    /// <summary>
    /// Executes a vector search in the specified scope. If an override options payload is provided,
    /// it will be merged with the defaults for the scope (without persisting).
    /// </summary>
    /// <param name="type">Scope type: PrimaryDocumentProcessLibrary or AdditionalDocumentLibrary.</param>
    /// <param name="shortName">Process short name or library short name.</param>
    /// <param name="q">Query text.</param>
    /// <param name="overrides">Optional override options.</param>
    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<List<VectorStoreSourceReferenceItemInfo>>> Search(
        [FromQuery] DocumentLibraryType type,
        [FromQuery] string shortName,
        [FromQuery(Name = "q")] string q,
        [FromBody] ConsolidatedSearchOptions? overrides)
    {
        if (string.IsNullOrWhiteSpace(shortName))
        {
            return BadRequest("shortName is required");
        }
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(new List<VectorStoreSourceReferenceItemInfo>());
        }

        switch (type)
        {
            case DocumentLibraryType.PrimaryDocumentProcessLibrary:
            {
                var process = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(shortName);
                if (process == null) return NotFound();
                var defaults = await _optionsFactory.CreateSearchOptionsForDocumentProcessAsync(process);
                var merged = MergeOptions(defaults, overrides);
                var repo = await _repositoryFactory.CreateForDocumentProcessAsync(process);
                var results = await repo.SearchAsync(shortName, q, merged);
                var mapped = results.Select(r => _mapper.Map<SourceReferenceItemInfo>(r)).OfType<VectorStoreSourceReferenceItemInfo>().ToList();
                ProxyLinks(mapped);
                return Ok(mapped);
            }
            case DocumentLibraryType.AdditionalDocumentLibrary:
            {
                var library = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(shortName);
                if (library == null) return NotFound();
                var defaults = await _optionsFactory.CreateSearchOptionsForDocumentLibraryAsync(library);
                var merged = MergeOptions(defaults, overrides);
                var repo = await _repositoryFactory.CreateForDocumentLibraryAsync(shortName);
                var results = await repo.SearchAsync(shortName, q, merged);
                var mapped = results.Select(r => _mapper.Map<SourceReferenceItemInfo>(r)).OfType<VectorStoreSourceReferenceItemInfo>().ToList();
                ProxyLinks(mapped);
                return Ok(mapped);
            }
            default:
                return BadRequest("Unsupported scope type for vector search.");
        }
    }

    private static ConsolidatedSearchOptions MergeOptions(ConsolidatedSearchOptions defaults, ConsolidatedSearchOptions? overrides)
    {
        if (overrides == null) return defaults;
        defaults.Top = overrides.Top > 0 ? overrides.Top : defaults.Top;
        defaults.MinRelevance = overrides.MinRelevance > 0 ? overrides.MinRelevance : defaults.MinRelevance;
        defaults.PrecedingPartitionCount = overrides.PrecedingPartitionCount >= 0 ? overrides.PrecedingPartitionCount : defaults.PrecedingPartitionCount;
        defaults.FollowingPartitionCount = overrides.FollowingPartitionCount >= 0 ? overrides.FollowingPartitionCount : defaults.FollowingPartitionCount;
        defaults.EnableProgressiveSearch = overrides.EnableProgressiveSearch;
        defaults.EnableKeywordFallback = overrides.EnableKeywordFallback;
        return defaults;
    }

    /// <summary>
    /// Ensure all source links are proxied via FileController for auth. If missing, try extract from chunk tags.
    /// If a chunk exposes a SourceDocumentSourcePage tag, include '?page=N' for deep-linking; otherwise omit.
    /// </summary>
    private static void ProxyLinks(List<VectorStoreSourceReferenceItemInfo> items)
    {
        foreach (var item in items)
        {
            var link = item.SourceReferenceLink;
            int? page = null;
            if (string.IsNullOrWhiteSpace(link))
            {
                // Try to infer from first chunk tags
                var first = item.Chunks?.FirstOrDefault();
                if (first?.Tags != null)
                {
                    foreach (var kvp in first.Tags)
                    {
                        if (kvp.Key.Contains("url", StringComparison.OrdinalIgnoreCase) || kvp.Key.Contains("link", StringComparison.OrdinalIgnoreCase))
                        {
                            var candidate = kvp.Value?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                            if (!string.IsNullOrWhiteSpace(candidate))
                            {
                                link = candidate;
                                break;
                            }
                        }
                    }
                    if (first.Tags.TryGetValue("SourceDocumentSourcePage", out var pages) && pages is { Count: > 0 })
                    {
                        if (int.TryParse(pages[0], out var p)) page = p;
                    }
                }
            }
            else
            {
                // Still try to get page from the first chunk even when link exists
                var first = item.Chunks?.FirstOrDefault();
                if (first?.Tags != null && first.Tags.TryGetValue("SourceDocumentSourcePage", out var pages) && pages is { Count: > 0 })
                {
                    if (int.TryParse(pages[0], out var p)) page = p;
                }
            }

            if (!string.IsNullOrWhiteSpace(link))
            {
                item.SourceReferenceLink = EnsureProxied(link!, page);
            }
        }

        static string EnsureProxied(string url, int? page)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
            if (url.StartsWith("/api/file/download/", StringComparison.OrdinalIgnoreCase))
            {
                // Append page if provided and not already present
                if (page.HasValue && !url.Contains("?", StringComparison.Ordinal))
                {
                    return url + $"?disposition=inline&page={page.Value}";
                }
                return url;
            }
            var encoded = Uri.EscapeDataString(url);
            var pageSuffix = page.HasValue ? $"&page={page.Value}" : string.Empty;
            return $"/api/file/download/{encoded}?disposition=inline{pageSuffix}";
        }
    }
}
