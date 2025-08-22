// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Manages Tesseract OCR language data files (traineddata) by ensuring required languages
/// are available locally. Sources: Azure Blob storage and optional external repository (GitHub tessdata).
/// Files are cached under the process-specific temporary directory.
/// </summary>
public class TesseractLanguageManager
{
    private readonly ILogger<TesseractLanguageManager> _logger;
    private readonly IOptionsSnapshot<ServiceConfigurationOptions> _options;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly HttpClient _httpClient;
    private readonly string _tessdataPath;
    private static readonly SemaphoreSlim _downloadLock = new(1, 1);

    public TesseractLanguageManager(
        ILogger<TesseractLanguageManager> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> options,
        AzureFileHelper azureFileHelper)
    {
        _logger = logger;
        _options = options;
        _azureFileHelper = azureFileHelper;
        _httpClient = new HttpClient();
        _tessdataPath = BuildTessdataPath();
        Directory.CreateDirectory(_tessdataPath);
    }

    /// <summary>
    /// Returns the local tessdata path where traineddata files are stored.
    /// </summary>
    public string GetTessdataPath() => _tessdataPath;

    /// <summary>
    /// Ensures all specified languages exist locally; downloads missing files from blob storage
    /// or, if allowed, from an external repository.
    /// </summary>
    public async Task EnsureLanguagesAvailableAsync(IEnumerable<string> languages, CancellationToken cancellationToken = default)
    {
        var langs = languages?.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    ?? new List<string>();
        if (langs.Count == 0) { return; }

        foreach (var lang in langs)
        {
            var targetFile = Path.Combine(_tessdataPath, $"{lang}.traineddata");
            if (File.Exists(targetFile)) { continue; }

            await _downloadLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (File.Exists(targetFile)) { continue; }

                // Try Azure Blob first
                if (await TryDownloadFromBlobAsync(lang, targetFile, cancellationToken))
                {
                    _logger.LogInformation("Downloaded tessdata for '{Lang}' from blob to {Path}", lang, targetFile);
                    continue;
                }

                // Try external repo if allowed
                if (_options.Value.GreenlightServices.DocumentIngestion.Ocr.AllowExternalDownloads)
                {
                    if (await TryDownloadFromExternalAsync(lang, targetFile, cancellationToken))
                    {
                        _logger.LogInformation("Downloaded tessdata for '{Lang}' from external repo to {Path}", lang, targetFile);
                        continue;
                    }
                }

                _logger.LogWarning("Tessdata for '{Lang}' not found in blob or external repo; OCR may not work for this language.", lang);
            }
            finally
            {
                _downloadLock.Release();
            }
        }
    }

    private async Task<bool> TryDownloadFromBlobAsync(string lang, string targetFile, CancellationToken ct)
    {
        try
        {
            var container = _options.Value.GreenlightServices.DocumentIngestion.Ocr.TessdataBlobContainer;
            // We use a flat naming scheme: "<lang>.traineddata"
            await using var stream = await _azureFileHelper.GetFileAsStreamFromContainerAndBlobName(container, $"{lang}.traineddata");
            if (stream == null) { return false; }
            await using var fs = File.Create(targetFile);
            await stream.CopyToAsync(fs, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to download tessdata for '{Lang}' from blob", lang);
            return false;
        }
    }

    private async Task<bool> TryDownloadFromExternalAsync(string lang, string targetFile, CancellationToken ct)
    {
        try
        {
            var baseUrl = _options.Value.GreenlightServices.DocumentIngestion.Ocr.ExternalRepoBaseUrl?.TrimEnd('/')
                          ?? "https://github.com/tesseract-ocr/tessdata/raw/main";
            var url = $"{baseUrl}/{WebUtility.UrlEncode(lang)}.traineddata";
            using var resp = await _httpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) { return false; }
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            await using var fs = File.Create(targetFile);
            await stream.CopyToAsync(fs, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to download tessdata for '{Lang}' from external repo", lang);
            return false;
        }
    }

    private static string BuildTessdataPath()
    {
        var directoryElements = new List<string>
        {
            "greenlight-ocr",
            Environment.MachineName,
            AppDomain.CurrentDomain.FriendlyName,
            "process-" + Environment.ProcessId.ToString(),
            "tessdata"
        };

        return Path.Combine(Path.GetTempPath(), Path.Combine(directoryElements.ToArray()));
    }
}
