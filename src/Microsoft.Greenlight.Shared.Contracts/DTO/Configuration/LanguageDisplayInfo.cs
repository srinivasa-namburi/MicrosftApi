// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;

/// <summary>
/// Info for a language intended for UI display.
/// </summary>
public class LanguageDisplayInfo
{
    /// <summary>
    /// Tesseract language code (e.g., "eng").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// English human-readable name (e.g., "English").
    /// </summary>
    public string EnglishName { get; set; } = string.Empty;

    /// <summary>
    /// Combined label (e.g., "English (eng)").
    /// </summary>
    public string Label { get; set; } = string.Empty;
}
