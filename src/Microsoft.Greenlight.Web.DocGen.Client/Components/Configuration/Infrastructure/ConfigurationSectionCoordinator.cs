// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Components;

namespace Microsoft.Greenlight.Web.DocGen.Client.Components.Configuration.Infrastructure;

/// <summary>
/// Coordinates multiple configuration components for dirty-state and save aggregation.
/// </summary>
public class ConfigurationSectionCoordinator
{
    private readonly List<IConfigurationSectionComponent> _components = new();

    public event Action? DirtyChanged;

    public void Register(IConfigurationSectionComponent component)
    {
        if (_components.Contains(component))
        {
            return;
        }

        _components.Add(component);
        component.DirtyChanged += _ => NotifyDirtyChanged();
    }

    internal void NotifyDirtyChanged()
    {
        DirtyChanged?.Invoke();
    }

    public bool HasAnyDirty => _components.Any(c => c.IsDirty);

    public async Task SaveAllAsync(IEnumerable<string>? prioritizeTitles = null, CancellationToken cancellationToken = default)
    {
        var ordered = _components.ToList();
        if (prioritizeTitles != null)
        {
            var titleOrder = prioritizeTitles.ToList();
            ordered = ordered
                .OrderBy(c =>
                {
                    var idx = titleOrder.FindIndex(t => string.Equals(t, c.Title, StringComparison.OrdinalIgnoreCase));
                    return idx < 0 ? int.MaxValue : idx;
                })
                .ToList();
        }

        foreach (var component in ordered)
        {
            if (component.IsDirty)
            {
                await component.SaveAsync(cancellationToken);
            }
        }
    }

    public async Task<string> BuildUnifiedSnackbarAsync(CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        foreach (var component in _components)
        {
            if (component.IsDirty)
            {
                var section = component.Title;
                var entries = component.DescribePendingChanges()?.ToList() ?? new List<string>();
                if (entries.Count == 0)
                {
                    lines.Add($"- {section}: updated");
                }
                else
                {
                    lines.Add($"- {section}: {entries.Count} change(s)");
                    foreach (var e in entries.Take(5))
                    {
                        lines.Add($"  • {e}");
                    }
                    if (entries.Count > 5)
                    {
                        lines.Add($"  • … {entries.Count - 5} more");
                    }
                }
            }
        }
        if (lines.Count == 0)
        {
            return "No changes to save.";
        }
        return string.Join("\n", lines);
    }
}
