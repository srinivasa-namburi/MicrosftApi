// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Basic text extraction service supporting common file formats.
/// </summary>
public class BasicTextExtractionService : ITextExtractionService
{
    private readonly ILogger<BasicTextExtractionService> _logger;

    public BasicTextExtractionService(ILogger<BasicTextExtractionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ExtractTextAsync(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        switch (extension)
        {
            case ".txt":
            case ".md":
            case ".csv":
                return await ExtractPlainTextAsync(fileStream);

            case ".html":
            case ".htm":
                return await ExtractHtmlTextAsync(fileStream);

            case ".pdf":
                _logger.LogWarning("PDF extraction not yet implemented for file {FileName}", fileName);
                return string.Empty;

            case ".docx":
                _logger.LogWarning("DOCX extraction not yet implemented for file {FileName}", fileName);
                return string.Empty;

            default:
                _logger.LogWarning("Unsupported file type {Extension} for file {FileName}", extension, fileName);
                return string.Empty;
        }
    }

    /// <inheritdoc />
    public bool SupportsFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" or ".md" or ".csv" or ".html" or ".htm" => true,
            _ => false
        };
    }

    private async Task<string> ExtractPlainTextAsync(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private async Task<string> ExtractHtmlTextAsync(Stream fileStream)
    {
        // Basic HTML text extraction (strips tags)
        var html = await ExtractPlainTextAsync(fileStream);

        // Remove HTML tags - this is a very basic implementation
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

        return text.Trim();
    }
}
