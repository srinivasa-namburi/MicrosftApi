using Microsoft.SemanticKernel;
using System.Collections.Concurrent;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Holds cached Semantic Kernel instances per document process / validation / generic model.
    /// Kernels are cloned before use to avoid state bleed while preserving configuration and loaded plugins.
    /// </summary>
    public class SemanticKernelInstanceContainer
    {
        /// <summary>
        /// Kernels configured for standard (non-validation) document process operations keyed by document process short name.
        /// </summary>
        public ConcurrentDictionary<string, Kernel> StandardKernels { get; } = new();
        /// <summary>
        /// Kernels configured for validation operations keyed by document process short name.
        /// </summary>
        public ConcurrentDictionary<string, Kernel> ValidationKernels { get; } = new();
        /// <summary>
        /// Generic kernels keyed by model identifier (e.g. deployment name) without document process plugins.
        /// </summary>
        public ConcurrentDictionary<string, Kernel> GenericKernels { get; } = new();
    }
}