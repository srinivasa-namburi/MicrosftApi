namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Specifies the type of source reference link.
/// </summary>
public enum SourceReferenceLinkType
{
    /// <summary>
    /// An external anonymous URL.
    /// </summary>
    ExternalAnonymousUrl = 100,

    /// <summary>
    /// A system proxied URL.
    /// </summary>
    SystemProxiedUrl = 200,

    /// <summary>
    /// A system non-proxied URL.
    /// </summary>
    SystemNonProxiedUrl = 300
}
