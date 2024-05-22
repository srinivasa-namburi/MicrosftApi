using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Interfaces;

namespace ProjectVico.V2.DocumentProcess.Shared.Plugins.KmDocs;

public class KmDocsPlugin : IPluginImplementation
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DocumentProcessInfo _documentProcess;
    private IKernelMemoryRepository? _kernelMemoryRepository;
    private IKernelMemory? _kernelMemory;

    /// <summary>
    /// This class needs to be constructed per Document Process, which is handled in the paired PluginRegistration class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="documentProcess"></param>
    public KmDocsPlugin(IServiceProvider serviceProvider, DocumentProcessInfo documentProcess)
    {
        _serviceProvider = serviceProvider;
        _documentProcess = documentProcess;
        Initialize();
    }

    private void Initialize()
    {
        _kernelMemory = _serviceProvider.GetKeyedService<IKernelMemory>(_documentProcess.ShortName + "-IKernelMemory");
        _kernelMemoryRepository = _serviceProvider.GetKeyedService<IKernelMemoryRepository>(_documentProcess.ShortName + "-IKernelMemoryRepository");
    }

    [KernelFunction(nameof(AskQuestionAsync))]
    [Description("Ask a question to the underlying document process knowledge base")]
    public async Task<string> AskQuestionAsync(
        [Description("The question to ask the document repository. Make sure to format as a proper question ending with a question mark")]
        string question)
    {
        if (_kernelMemory == null)
        {
            throw new InvalidOperationException("Kernel Memory not found");
        }

        var index = _documentProcess.Repositories[0];

        var response = await _kernelMemory.AskAsync(question, index: index);

        var responseText = response.Result;

        return string.IsNullOrEmpty(responseText) ? "I'm sorry, I don't have an answer for that." : responseText;
    }



}