using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Web.DocGen.Client.Components;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// This holds a registry of ContentNodeBodyTextComponent components, used for accessing the components from other components.
/// </summary>
public static class ContentNodeBodyTextComponentRegistry
{
    private static readonly Dictionary<Guid, List<ContentNodeBodyTextComponent>> _components = new();

    public static void RegisterComponent(Guid parentId, ContentNodeBodyTextComponent component)
    {
        if (!_components.TryGetValue(parentId, out var list))
        {
            list = new List<ContentNodeBodyTextComponent>();
            _components[parentId] = list;
        }

        list.Add(component);
    }

    public static void UnregisterComponent(Guid parentId, ContentNodeBodyTextComponent component)
    {
        if (_components.TryGetValue(parentId, out var list))
        {
            list.Remove(component);
            if (list.Count == 0)
            {
                _components.Remove(parentId);
            }
        }
    }

    // New method to notify components of node updates
    public static void NotifyNodeUpdated(Guid nodeId, ContentNodeInfo updatedNode)
    {
        // Find components that might be showing this node or its children
        if (_components.TryGetValue(nodeId, out var components))
        {
            foreach (var component in components)
            {
                component.NotifyParentNodeUpdated(updatedNode);
            }
        }
    }
}