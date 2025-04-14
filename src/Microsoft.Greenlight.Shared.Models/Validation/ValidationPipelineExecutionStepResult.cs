using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.Validation
{
    public class ValidationPipelineExecutionStepResult : EntityBase
    {
        public required Guid ValidationPipelineExecutionStepId { get; set; }
        [JsonIgnore]
        public ValidationPipelineExecutionStep? ValidationPipelineExecutionStep { get; set; }

        public List<ValidationExecutionStepContentNodeResult> ContentNodeResults { get; set; } = [];

    }
}