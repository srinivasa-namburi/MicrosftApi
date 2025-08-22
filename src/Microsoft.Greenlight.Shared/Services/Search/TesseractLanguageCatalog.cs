// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Globalization;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Provides mapping from Tesseract language codes to human-readable English names.
/// Covers common languages and special Tesseract codes. Falls back to CultureInfo and the raw code.
/// </summary>
public static class TesseractLanguageCatalog
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        // Special Tesseract data packs
        { "osd", "Orientation and Script Detection" },
        { "equ", "Math / Equation Detection" },
        // Chinese variants in tessdata
        { "chi_sim", "Chinese (Simplified)" },
        { "chi_tra", "Chinese (Traditional)" },
        // Major languages (ISO-639-2/3 codes used by Tesseract)
        { "eng", "English" },
        { "deu", "German" },
        { "ger", "German" }, // legacy synonym
        { "fra", "French" },
        { "fre", "French" }, // legacy synonym
        { "spa", "Spanish" },
        { "por", "Portuguese" },
        { "ita", "Italian" },
        { "nld", "Dutch" },
        { "dut", "Dutch" }, // legacy synonym
        { "nor", "Norwegian" },
        { "swe", "Swedish" },
        { "dan", "Danish" },
        { "fin", "Finnish" },
        { "pol", "Polish" },
        { "ces", "Czech" },
        { "cze", "Czech" }, // legacy synonym
        { "slk", "Slovak" },
        { "hun", "Hungarian" },
        { "ron", "Romanian" },
        { "rum", "Romanian" }, // legacy synonym
        { "ukr", "Ukrainian" },
        { "bul", "Bulgarian" },
        { "srp", "Serbian" },
        { "hrv", "Croatian" },
        { "slv", "Slovenian" },
        { "ell", "Greek" },
        { "gre", "Greek" }, // legacy synonym
        { "rus", "Russian" },
        { "tur", "Turkish" },
        { "heb", "Hebrew" },
        { "ara", "Arabic" },
        { "tha", "Thai" },
        { "vie", "Vietnamese" },
        { "jpn", "Japanese" },
        { "kor", "Korean" },
        { "hin", "Hindi" },
        { "ben", "Bengali" },
        { "tam", "Tamil" },
        { "tel", "Telugu" },
        { "mar", "Marathi" },
        { "urd", "Urdu" },
        { "pes", "Persian" },
        { "fas", "Persian" }, // synonym
        { "ind", "Indonesian" },
        { "msa", "Malay" },
        { "tgl", "Tagalog" },
        { "fil", "Filipino" },
        { "cat", "Catalan" },
        { "glg", "Galician" },
        { "eus", "Basque" },
        { "isl", "Icelandic" },
        { "lav", "Latvian" },
        { "lit", "Lithuanian" },
        { "est", "Estonian" },
        { "afr", "Afrikaans" },
        { "srp_latn", "Serbian (Latin)" },
        { "srp_cyrl", "Serbian (Cyrillic)" }
    };

    /// <summary>
    /// Gets the English display name for a Tesseract language code.
    /// </summary>
    public static string GetEnglishName(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        if (Known.TryGetValue(code, out var name))
        {
            return name;
        }

        // Try BCP-47 or two-letter ISO if possible by heuristic mapping
        // For three-letter ISO codes, try best-effort mapping via CultureInfo if available
        try
        {
            // Try exact
            var ci = CultureInfo.GetCultures(CultureTypes.AllCultures).FirstOrDefault(c => string.Equals(c.Name, code, StringComparison.OrdinalIgnoreCase));
            if (ci != null && !string.IsNullOrWhiteSpace(ci.EnglishName))
            {
                return ci.EnglishName;
            }
        }
        catch
        {
            // ignored
        }

        // Fallback: uppercase the code
        return code.ToLowerInvariant() switch
        {
            "enm" => "Middle English",
            _ => code
        };
    }

    /// <summary>
    /// Gets a display label like "English (eng)" for a code.
    /// </summary>
    public static string GetDisplayLabel(string code)
    {
        var name = GetEnglishName(code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return name;
        }
        return string.IsNullOrWhiteSpace(name) || string.Equals(name, code, StringComparison.OrdinalIgnoreCase)
            ? code
            : $"{name} ({code})";
    }
}
