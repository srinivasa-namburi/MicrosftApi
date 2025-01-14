namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents the various states of ingestion process of documents.
/// </summary>
public enum IngestionState
{
    /// <summary>
    /// The file has been uploaded.
    /// </summary>
    Uploaded = 100,

    /// <summary>
    /// The file is being classified.
    /// </summary>
    Classifying = 200,

    /// <summary>
    /// The file is being processed.
    /// </summary>
    Processing = 300,

    /// <summary>
    /// The ingestion process is complete.
    /// </summary>
    Complete = 800,

    /// <summary>
    /// The file classification is unsupported.
    /// </summary>
    ClassificationUnsupported = 850,

    /// <summary>
    /// The ingestion process has failed.
    /// </summary>
    Failed = 900
}
