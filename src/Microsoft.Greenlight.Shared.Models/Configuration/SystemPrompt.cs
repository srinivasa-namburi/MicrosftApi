// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Models.Configuration;

/// <summary>
/// Represents a system-wide prompt override stored in the database.
/// If no override exists for a prompt name, the default from SystemWidePromptCatalogTemplates is used.
/// </summary>
public sealed class SystemPrompt : EntityBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SystemPrompt"/> class with a new GUID.
    /// </summary>
    public SystemPrompt() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemPrompt"/> class with the specified GUID.
    /// </summary>
    /// <param name="id">The unique identifier for the system prompt.</param>
    public SystemPrompt(Guid id) : base(id)
    {
    }

    /// <summary>
    /// Unique name matching property name in SystemWidePromptCatalogTemplates.
    /// Examples: "FlowBackendConversationSystemPrompt", "FlowUserConversationSystemPrompt"
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Override text for the system prompt. If null/empty, default from catalog is used.
    /// May contain Scriban template variables (e.g., {{ variableName }}).
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether this override is active. If false, reverts to default from catalog.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
