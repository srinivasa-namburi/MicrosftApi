// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;

/// <summary>
/// Response for an OCR language download operation.
/// </summary>
public class OcrLanguageDownloadResponse
{
    /// <summary>
    /// The Tesseract language code requested (e.g., "eng").
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// The absolute blob URL where the language file was stored.
    /// </summary>
    public string BlobUrl { get; set; } = string.Empty;
}
