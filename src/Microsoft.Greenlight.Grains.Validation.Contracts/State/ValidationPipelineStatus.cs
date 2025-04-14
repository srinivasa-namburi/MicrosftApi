namespace Microsoft.Greenlight.Grains.Validation.Contracts.State
{
    public enum ValidationPipelineStatus
    {
        NotStarted,
        Loading,
        Executing,
        Completed,
        Failed
    }
}