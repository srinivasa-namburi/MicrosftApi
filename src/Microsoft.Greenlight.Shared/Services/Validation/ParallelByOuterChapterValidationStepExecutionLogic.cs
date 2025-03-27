using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;

namespace Microsoft.Greenlight.Shared.Services.Validation
{
    /// <summary>
    /// Logic for executing a validation step that validates the document by outer chapters in parallel.
    /// </summary>
    public class ParallelByOuterChapterValidationStepExecutionLogic : IValidationStepExecutionLogic
    {
        private readonly ILogger<ParallelByOuterChapterValidationStepExecutionLogic> _logger;

        public ParallelByOuterChapterValidationStepExecutionLogic(ILogger<ParallelByOuterChapterValidationStepExecutionLogic> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(ExecuteValidationStep stepMessage)
        {
           _logger.LogWarning("ParallelByOuterChapterValidationStepExecutionLogic has not been implemented yet.");
           
        }
    }
}