using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.SagaState;

public class KernelMemoryDocumentIngestionSagaState : DocumentIngestionSagaState
{
    public DocumentLibraryType DocumentLibraryType { get; set; } = DocumentLibraryType.PrimaryDocumentProcessLibrary;
}
