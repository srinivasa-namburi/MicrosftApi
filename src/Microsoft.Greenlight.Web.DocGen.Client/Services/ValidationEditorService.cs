// Create new file: Microsoft.Greenlight.Web.DocGen.Client/Services/ValidationEditorService.cs

using Microsoft.Greenlight.Shared.Contracts.DTO.Validation;
using Microsoft.Greenlight.Web.DocGen.Client.Enums;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services
{
    public class ValidationEditorService
    {
        private readonly Dictionary<Guid, ValidationEditorData> _editorData = new();
        private readonly IContentNodeApiClient _contentNodeApiClient;
        private readonly ILogger<ValidationEditorService>? _logger;

        public event Action<Guid>? ValidationStatusChanged;

        public event Action<ValidationEditorStateChange>? StateChanged;

        public ValidationEditorService(
            IContentNodeApiClient contentNodeApiClient,
            ILogger<ValidationEditorService>? logger = null
            )
        {
            _contentNodeApiClient = contentNodeApiClient;
            _logger = logger;
        }

        public void SetEditorData(Guid contentNodeId, ValidationEditorData data)
        {
            _editorData[contentNodeId] = data;
            StateChanged?.Invoke(new ValidationEditorStateChange(contentNodeId, data));
        }

        public ValidationEditorData? GetEditorData(Guid contentNodeId)
        {
            return _editorData.TryGetValue(contentNodeId, out var data) ? data : null;
        }

        public async Task<string?> GetValidationSuggestedTextAsync(Guid contentNodeId)
        {
            // First check if we have this content node's data in our cache
            foreach (var entry in _editorData)
            {
                // Check if this entry is related to our node
                var data = entry.Value;
                if (data.ValidationChangeInfo?.ResultantContentNodeId == contentNodeId)
                {
                    return data.SuggestedText;
                }
            }

            // If not found in cache by ResultantContentNodeId, check by direct key
            if (_editorData.TryGetValue(contentNodeId, out var directData))
            {
                return directData.SuggestedText;
            }

            try
            {
                // Fetch the content node from the API
                var contentNode = await _contentNodeApiClient.GetContentNodeAsync(contentNodeId.ToString());
                if (contentNode != null)
                {
                    // Use the text from the fetched content node as the suggested text
                    var suggestedText = contentNode.Text;

                    // We don't store this in cache since we don't have complete information
                    // (we're missing the ValidationChangeInfo)
                    return suggestedText;
                }
                else
                {
                    // Log a warning if the content node could not be fetched
                    _logger?.LogWarning($"Content node with ID {contentNodeId} could not be fetched.");
                }
            }
            catch (Exception ex)
            {
                // Log the exception for debugging purposes
                _logger?.LogError(ex, $"Error fetching content node with ID {contentNodeId}");
            }

            // Return null if the data could not be fetched
            return null;
        }

        public void NotifyValidationStatusChanged(Guid contentNodeId)
        {
            ValidationStatusChanged?.Invoke(contentNodeId);
        }

        public void ClearValidationStatus(Guid contentNodeId, Guid? parentSectionId)
        {
            // Clear data for this node
            ClearEditorData(contentNodeId);
    
            // Clear data for parent section if provided
            if (parentSectionId.HasValue)
            {
                ClearValidationDataForSection(parentSectionId.Value);
            }
    
            // Notify about the changes
            NotifyValidationStatusChanged(contentNodeId);
            if (parentSectionId.HasValue)
            {
                NotifyValidationStatusChanged(parentSectionId.Value);
            }
        }


        public void ClearEditorData(Guid contentNodeId)
        {
            if (_editorData.ContainsKey(contentNodeId))
            {
                _editorData.Remove(contentNodeId);
            }
        }

        public void ClearAllValidationData()
        {
            // Clear all entries in the dictionary
            _editorData.Clear();
        }

        public void ClearValidationDataForSection(Guid parentSectionId)
        {
            // Find all editor data entries that relate to this section
            var keysToRemove = _editorData
                .Where(kvp => kvp.Value.ValidationChangeInfo?.ParentContentNodeId == parentSectionId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                ClearEditorData(key);
            }
        }
    }

    public record ValidationEditorData(
        EditorComponentEditorMode EditorMode,
        string OriginalText,
        string SuggestedText,
        bool IsVisible,
        ValidationContentChangeInfo? ValidationChangeInfo
    );

    public record ValidationEditorStateChange(Guid ContentNodeId, ValidationEditorData Data);
}
