namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentProcesses;

/// <summary>
/// Command to create dynamic document process prompts.
/// </summary>
/// <param name="DocumentProcessId">The document process ID.</param>
public record CreateDynamicDocumentProcessPrompts(Guid DocumentProcessId);
