using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;

namespace Microsoft.Greenlight.Shared.Services.Validation
{
    /// <summary>
    /// Logic for executing a validation step that validates the entire document with all sections in parallel.
    /// </summary>
    public class ParallelFullDocumentValidationStepExecutionLogic : IValidationStepExecutionLogic
    {
        private readonly ILogger<ParallelByOuterChapterValidationStepExecutionLogic> _logger;

        public ParallelFullDocumentValidationStepExecutionLogic(ILogger<ParallelByOuterChapterValidationStepExecutionLogic> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(ExecuteValidationStep stepMessage)
        {
            _logger.LogWarning("ParallelFullDocumentValidationStepExecutionLogic has not been implemented yet.");
        }
    }
}