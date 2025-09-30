// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Components;

namespace Microsoft.Greenlight.Web.DocGen.Client.Components.Configuration.Infrastructure;

/// <summary>
/// Contract for a configuration section component that can report dirty-state and save its own content.
/// The hosting page may call <see cref="SaveAsync"/> as part of a global "Save All" action.
/// </summary>
public interface IConfigurationSectionComponent
{
    /// <summary>
    /// Display title for this section.
    /// </summary>
    string Title { get; }
    /// <summary>
    /// True if the component has unsaved changes.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Raised when the component's dirty state changes.
    /// </summary>
    event Action<bool>? DirtyChanged;

    /// <summary>
    /// Allows the component to initialize or reload its state.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the component's configuration and resets dirty state.
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes pending changes for this component (for a unified snackbar message).
    /// Only called when <see cref="IsDirty"/> is true.
    /// </summary>
    IEnumerable<string> DescribePendingChanges();
}
