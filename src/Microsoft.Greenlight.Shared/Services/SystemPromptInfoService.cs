// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Configuration;
using Microsoft.Greenlight.Shared.Prompts;
using Scriban;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// Service for managing system-wide prompts with database overrides.
/// Uses reflection to discover defaults from SystemWidePromptCatalogTemplates.
/// </summary>
public sealed class SystemPromptInfoService : ISystemPromptInfoService
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemPromptInfoService"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    public SystemPromptInfoService(IDbContextFactory<DocGenerationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<string?> GetPromptAsync(string promptName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            return null;
        }

        // First check for active database override
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dbOverride = await db.SystemPrompts
            .Where(p => p.Name == promptName && p.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (dbOverride != null && !string.IsNullOrWhiteSpace(dbOverride.Text))
        {
            return dbOverride.Text;
        }

        // Fall back to default from SystemWidePromptCatalogTemplates
        return GetDefaultPromptByName(promptName);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetAllPromptsAsync(CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();

        // Get all defaults from SystemWidePromptCatalogTemplates using reflection
        var defaults = GetAllDefaultPrompts();

        // Get all active database overrides
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var overrides = await db.SystemPrompts
            .Where(p => p.IsActive)
            .ToDictionaryAsync(p => p.Name, p => p.Text, cancellationToken);

        // Merge: database overrides take precedence
        foreach (var (name, text) in defaults)
        {
            result[name] = overrides.TryGetValue(name, out var overrideText) && !string.IsNullOrWhiteSpace(overrideText)
                ? overrideText
                : text;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Guid> UpsertPromptOverrideAsync(string promptName, string promptText, bool isActive = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            throw new ArgumentException("Prompt name cannot be empty", nameof(promptName));
        }

        if (string.IsNullOrWhiteSpace(promptText))
        {
            throw new ArgumentException("Prompt text cannot be empty", nameof(promptText));
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.SystemPrompts
            .Where(p => p.Name == promptName)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null)
        {
            // Update existing
            existing.Text = promptText;
            existing.IsActive = isActive;
            existing.ModifiedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }
        else
        {
            // Create new
            var newPrompt = new SystemPrompt
            {
                Name = promptName,
                Text = promptText,
                IsActive = isActive
            };
            db.SystemPrompts.Add(newPrompt);
            await db.SaveChangesAsync(cancellationToken);
            return newPrompt.Id;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeletePromptOverrideAsync(string promptName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            return false;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.SystemPrompts
            .Where(p => p.Name == promptName)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing == null)
        {
            return false;
        }

        db.SystemPrompts.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<string?> RenderPromptAsync(string promptName, Dictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        var promptText = await GetPromptAsync(promptName, cancellationToken);
        if (promptText == null)
        {
            return null;
        }

        // Render with Scriban
        var template = Template.Parse(promptText);
        if (template.HasErrors)
        {
            throw new InvalidOperationException($"Failed to parse Scriban template for prompt '{promptName}': {string.Join(", ", template.Messages)}");
        }

        var rendered = await template.RenderAsync(variables, member => member.Name);
        return rendered;
    }

    /// <summary>
    /// Gets the default prompt text from SystemWidePromptCatalogTemplates using reflection.
    /// </summary>
    /// <param name="promptName">The name of the prompt property (e.g., "FlowBackendConversationSystemPrompt").</param>
    /// <returns>The default prompt text, or null if not found.</returns>
    private static string? GetDefaultPromptByName(string promptName)
    {
        var templatesCatalogType = typeof(SystemWidePromptCatalogTemplates);
        var property = templatesCatalogType.GetProperty(promptName);

        if (property == null || property.PropertyType != typeof(string))
        {
            return null;
        }

        return property.GetValue(null) as string;
    }

    /// <summary>
    /// Gets all default prompts from SystemWidePromptCatalogTemplates using reflection.
    /// </summary>
    /// <returns>Dictionary mapping prompt names to their default text.</returns>
    private static Dictionary<string, string> GetAllDefaultPrompts()
    {
        var result = new Dictionary<string, string>();
        var templatesCatalogType = typeof(SystemWidePromptCatalogTemplates);

        var properties = templatesCatalogType.GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.CanRead);

        foreach (var property in properties)
        {
            var value = property.GetValue(null) as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                result[property.Name] = value;
            }
        }

        return result;
    }
}
