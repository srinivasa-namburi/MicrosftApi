// Copyright (c) Microsoft Corporation. All rights reserved.
// Note: Task may be available via implicit global usings; no explicit usings required here.

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Helper to temporarily set the ambient user context (ProviderSubjectId) for the duration of a delegate.
    /// Reduces boilerplate when flowing per-user context via AsyncLocal in grains and services.
    /// </summary>
    public static class UserContextRunner
    {
        /// <summary>
        /// Runs an async operation with the provided ProviderSubjectId set in the ambient context.
        /// Restores the previous value afterwards.
        /// </summary>
        public static async Task RunAsync(string? providerSubjectId, Func<Task> action)
        {
            var prev = UserExecutionContext.ProviderSubjectId;
            if (!string.IsNullOrWhiteSpace(providerSubjectId))
            {
                UserExecutionContext.ProviderSubjectId = providerSubjectId;
            }

            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                UserExecutionContext.ProviderSubjectId = prev;
            }
        }

        /// <summary>
        /// Runs an async operation with the provided ProviderSubjectId set in the ambient context and returns a result.
        /// Restores the previous value afterwards.
        /// </summary>
        public static async Task<T> RunAsync<T>(string? providerSubjectId, Func<Task<T>> func)
        {
            var prev = UserExecutionContext.ProviderSubjectId;
            if (!string.IsNullOrWhiteSpace(providerSubjectId))
            {
                UserExecutionContext.ProviderSubjectId = providerSubjectId;
            }

            try
            {
                return await func().ConfigureAwait(false);
            }
            finally
            {
                UserExecutionContext.ProviderSubjectId = prev;
            }
        }
    }
}
