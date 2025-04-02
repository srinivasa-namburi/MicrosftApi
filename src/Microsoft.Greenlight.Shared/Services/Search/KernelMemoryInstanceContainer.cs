using Microsoft.KernelMemory;
using System.Collections.Concurrent;

namespace Microsoft.Greenlight.Shared.Services.Search;

public class KernelMemoryInstanceContainer
{
    public ConcurrentDictionary<string, IKernelMemory> KernelMemoryInstances { get; set; } = new();
}