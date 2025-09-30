// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Components;

namespace Microsoft.Greenlight.Web.DocGen.Client.Components.Configuration.Infrastructure;

/// <summary>
/// Base class for configuration section components with standardized dirty reporting and save plan aggregation hooks.
/// </summary>
public abstract class ConfigurationSectionBase : ComponentBase, IConfigurationSectionComponent
{
    [CascadingParameter]
    public ConfigurationSectionCoordinator? Coordinator { get; set; }

    public virtual string Title => GetType().Name;
    public bool IsDirty => _isDirty;
    public event Action<bool>? DirtyChanged;

    private bool _isDirty;

    protected override void OnInitialized()
    {
        Coordinator?.Register(this);
        base.OnInitialized();
    }

    protected void SetDirty(bool value)
    {
        if (_isDirty == value)
        {
            return;
        }
        _isDirty = value;
        DirtyChanged?.Invoke(_isDirty);
        Coordinator?.NotifyDirtyChanged();
        StateHasChanged();
    }

    public virtual Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public virtual IEnumerable<string> DescribePendingChanges() => Array.Empty<string>();
}
