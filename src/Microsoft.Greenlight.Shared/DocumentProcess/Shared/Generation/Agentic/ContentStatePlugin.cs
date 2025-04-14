using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic;

public class ContentStatePlugin
{
    private readonly SortedDictionary<int, string> _documentParts = new();
    private readonly string _sourceDocuments;
    private readonly int _blockSize;
    
    public ContentStatePlugin(string sourceDocuments, int blockSize)
    {
        _sourceDocuments = sourceDocuments;
        _blockSize = blockSize;
    }

    [KernelFunction, Description("Get the content for a specific sequence number")]
    public string GetSequenceContent(int sequenceNumber)
    {
        return _documentParts.TryGetValue(sequenceNumber, out var content) ? content : string.Empty;
    }

    [KernelFunction, Description("Get all content assembled in order")]
    public string GetAssembledContent()
    {
        return string.Join("\n\n", _documentParts.Values);
    }

    [KernelFunction, Description("Store content for a specific sequence")]
    public void StoreSequenceContent(int sequenceNumber, string content)
    {
        _documentParts[sequenceNumber] = content;
    }

    [KernelFunction, Description("Remove a specific sequence")]
    public void RemoveSequenceContent(int sequenceNumber)
    {
        if (_documentParts.ContainsKey(sequenceNumber))
        {
            _documentParts.Remove(sequenceNumber);
        }
    }

    [KernelFunction, Description("Get all source documents")]
    public string GetSourceDocuments()
    {
        return _sourceDocuments;
    }

    [KernelFunction, Description("Get all sequence numbers in use")]
    public string GetSequenceNumbers()
    {
        return string.Join(", ", _documentParts.Keys);
    }

    [KernelFunction, Description("Gets the next available sequence number for producing content output")]
    public string GetNextSequenceNumber()
    {
        // Get the last sequence number
        var lastSequenceNumber = _documentParts.Keys.Count > 0 ? _documentParts.Keys.Max() : 0;

        // Return the next sequence number
        return (lastSequenceNumber + _blockSize).ToString();
    }

    [KernelFunction, Description("Get content with surrounding context")]
    public string GetSequenceWithContext(int sequenceNumber)
    {
        var surroundingContent = new StringBuilder();
        
        // Get previous sequence if it exists
        if (_documentParts.TryGetValue(sequenceNumber - _blockSize, out var prevContent))
        {
            surroundingContent.AppendLine("Previous content:");
            surroundingContent.AppendLine(prevContent);
        }
        
        // Get current sequence
        if (_documentParts.TryGetValue(sequenceNumber, out var currentContent))
        {
            surroundingContent.AppendLine("Current content:");
            surroundingContent.AppendLine(currentContent);
        }
        
        // Get next sequence if it exists
        if (_documentParts.TryGetValue(sequenceNumber + _blockSize, out var nextContent))
        {
            surroundingContent.AppendLine("Next content:");
            surroundingContent.AppendLine(nextContent);
        }
        
        return surroundingContent.ToString();
    }
}