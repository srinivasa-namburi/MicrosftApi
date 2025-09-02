namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Provides access to the current user's ProviderSubjectId in server-side services.
    /// </summary>
    public interface IUserContextAccessor
    {
        string? ProviderSubjectId { get; }
    }

    /// <summary>
    /// Default implementation that reads from ambient UserExecutionContext.
    /// </summary>
    public sealed class AmbientUserContextAccessor : IUserContextAccessor
    {
        public string? ProviderSubjectId => UserExecutionContext.ProviderSubjectId;
    }
}
