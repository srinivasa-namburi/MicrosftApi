namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Handles sync of code-based and database stored prompt definitions.
/// </summary>
public interface IPromptDefinitionService
{
    /// <summary>
    /// Ensures prompt definitions exist in the database for all default prompt types.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task EnsurePromptDefinitionsAsync(CancellationToken cancellationToken = default);
}