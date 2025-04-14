using Microsoft.SemanticKernel;
using System.Collections.Concurrent;

namespace Microsoft.Greenlight.Shared.Services.Microsoft.Greenlight.Shared.Services
{
    public class SemanticKernelInstanceContainer
    {
        public ConcurrentDictionary<string, Kernel> StandardKernels { get; } = new();
        public ConcurrentDictionary<string, Kernel> ValidationKernels { get; } = new();
        public ConcurrentDictionary<string, Kernel> GenericKernels { get; } = new();
    }
}