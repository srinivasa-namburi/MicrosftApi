namespace Microsoft.Greenlight.Shared.Enums;

public enum IngestionState
{
    Uploaded = 100,
    Classifying = 200,
    Processing = 300,
    Complete = 800,
    ClassificationUnsupported = 850,
    Failed = 900
}
