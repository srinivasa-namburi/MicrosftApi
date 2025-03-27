using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;

namespace Microsoft.Greenlight.Shared.Services.Validation
{
    /// <summary>
    /// Interface for the logic that executes a validation step.
    /// </summary>
    public interface IValidationStepExecutionLogic
    {
        /// <summary>
        /// Executes the validation step.
        /// </summary>
        /// <param name="step">An ExecuteValidationStep message</param>
        /// <returns></returns>
        Task ExecuteAsync(ExecuteValidationStep stepMessage);
    }
}