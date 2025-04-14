namespace Microsoft.Greenlight.Grains.Document.Contracts.Models;

public enum DocumentGenerationStatus
{
    Pending,
    Creating,
    Processing,
    ContentGeneration,
    ContentFinalized,
    Failed,
    Completed
}