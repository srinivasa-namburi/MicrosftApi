using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.SagaState;

/// <summary>
/// Represents the state of the Kernel Memory Document Ingestion Saga.
/// </summary>
public class KernelMemoryDocumentIngestionSagaState : DocumentIngestionSagaState
{
    /// <summary>
    /// Gets or sets the type of the document library.
    /// </summary>
    public DocumentLibraryType DocumentLibraryType { get; set; } = DocumentLibraryType.PrimaryDocumentProcessLibrary;
}
