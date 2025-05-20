// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Grains.Document.Contracts.State;
using System.Text;

namespace Microsoft.Greenlight.Grains.Document
{
    /// <summary>
    /// Orleans grain for managing shared content state for a document section.
    /// </summary>
    public class ContentStateGrain : Grain, IContentStateGrain
    {
        private readonly IPersistentState<ContentStateGrainState> _state;

        public ContentStateGrain([PersistentState("contentState")] IPersistentState<ContentStateGrainState> state)
        {
            _state = state;
        }

        public async Task SetSourceDocumentsAsync(string sourceDocuments, int blockSize)
        {
            _state.State.SourceDocuments = sourceDocuments;
            _state.State.BlockSize = blockSize;
            await _state.WriteStateAsync();
        }

        public Task<string> GetSourceDocumentsAsync()
        {
            return Task.FromResult(_state.State.SourceDocuments);
        }

        public async Task StoreSequenceContentAsync(int sequenceNumber, string content)
        {
            _state.State.DocumentParts[sequenceNumber] = content;
            await _state.WriteStateAsync();
        }

        public Task<string> GetSequenceContentAsync(int sequenceNumber)
        {
            return Task.FromResult(_state.State.DocumentParts.TryGetValue(sequenceNumber, out var content) ? content : string.Empty);
        }

        public async Task RemoveSequenceContentAsync(int sequenceNumber)
        {
            if (_state.State.DocumentParts.ContainsKey(sequenceNumber))
            {
                _state.State.DocumentParts.Remove(sequenceNumber);
                await _state.WriteStateAsync();
            }
        }

        public Task<string> GetAssembledContentAsync()
        {
            var result = string.Join("\n\n", _state.State.DocumentParts.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value));
            return Task.FromResult(result);
        }

        public Task<string> GetSequenceNumbersAsync()
        {
            var result = string.Join(", ", _state.State.DocumentParts.Keys.OrderBy(x => x));
            return Task.FromResult(result);
        }

        public Task<string> GetNextSequenceNumberAsync()
        {
            var lastSequenceNumber = _state.State.DocumentParts.Keys.Count > 0 ? _state.State.DocumentParts.Keys.Max() : 0;
            return Task.FromResult((lastSequenceNumber + _state.State.BlockSize).ToString());
        }

        public Task<string> GetSequenceWithContextAsync(int sequenceNumber)
        {
            var sb = new StringBuilder();
            var blockSize = _state.State.BlockSize;
            var parts = _state.State.DocumentParts;
            if (parts.TryGetValue(sequenceNumber - blockSize, out var prevContent))
            {
                sb.AppendLine("Previous content:");
                sb.AppendLine(prevContent);
            }
            if (parts.TryGetValue(sequenceNumber, out var currentContent))
            {
                sb.AppendLine("Current content:");
                sb.AppendLine(currentContent);
            }
            if (parts.TryGetValue(sequenceNumber + blockSize, out var nextContent))
            {
                sb.AppendLine("Next content:");
                sb.AppendLine(nextContent);
            }
            return Task.FromResult(sb.ToString());
        }

        public async Task ClearAndDeactivateAsync()
        {
            _state.State.DocumentParts.Clear();
            _state.State.SourceDocuments = string.Empty;
            _state.State.BlockSize = 100;
            await _state.WriteStateAsync();
            DeactivateOnIdle();
        }
    }
}
