namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Constants and helpers for flowing per-user context through Kernel.Data and ambient AsyncLocal context.
    /// </summary>
    public static class KernelUserContextConstants
    {
        /// <summary>
        /// Key used in Kernel.Data to store the Provider Subject ID (OID/sub).
        /// </summary>
        public const string ProviderSubjectId = "gl.user.providersubjectid";
    }

    /// <summary>
    /// Ambient per-execution user context. Use sparingly to bridge places where passing state explicitly is hard.
    /// </summary>
    public static class UserExecutionContext
    {
        private static readonly AsyncLocal<string?> _providerSubjectId = new AsyncLocal<string?>();

        /// <summary>
        /// Gets or sets the ambient ProviderSubjectId for the current async flow.
        /// </summary>
        public static string? ProviderSubjectId
        {
            get => _providerSubjectId.Value;
            set => _providerSubjectId.Value = value;
        }
    }
}
