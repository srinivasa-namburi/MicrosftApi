// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;

namespace Microsoft.Greenlight.Grains.Chat.Agentic;

/// <summary>
/// Provides helper methods for constructing agent prompt context from Flow Task templates.
/// </summary>
internal static class FlowTaskAgentPromptBuilder
{
    /// <summary>
    /// Builds a template context summary used to orient the conversation agent.
    /// </summary>
    /// <param name="template">The Flow Task template.</param>
    /// <returns>A human-readable summary of the template.</returns>
    public static string BuildTemplateContextForInstructions(FlowTaskTemplateDetailDto? template)
    {
        if (template == null)
        {
            return "Template information is not available.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Template: {template.DisplayName}");
        if (!string.IsNullOrWhiteSpace(template.Description))
        {
            sb.AppendLine($"Description: {template.Description}");
        }

        if (template.Sections == null || !template.Sections.Any())
        {
            return sb.ToString();
        }

        sb.AppendLine();
        sb.AppendLine("Sections and requirements:");

        foreach (var section in template.Sections.OrderBy(s => s.SortOrder))
        {
            sb.AppendLine($"- {section.DisplayName} (Section: {section.Name})");

            if (section.Requirements == null)
            {
                continue;
            }

            foreach (var requirement in section.Requirements.OrderBy(r => r.SortOrder))
            {
                var requirementLine = $"  - {requirement.DisplayName} (Field: {requirement.FieldName})";
                if (requirement.IsRequired)
                {
                    requirementLine += " [Required]";
                }

                sb.AppendLine(requirementLine);
                if (!string.IsNullOrWhiteSpace(requirement.Description))
                {
                    sb.AppendLine($"    Description: {requirement.Description}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds instructions for the requirement collector agent outlining state grain APIs.
    /// </summary>
    /// <param name="template">The Flow Task template.</param>
    /// <returns>A formatted description of available state grain operations.</returns>
    public static string BuildFieldMappingsForExtraction(FlowTaskTemplateDetailDto? template)
    {
        if (template == null)
        {
            return "No requirement mappings available because the template failed to load.";
        }

        var mappings = new StringBuilder();
        mappings.AppendLine("Requirement field mappings (use these helper methods on FlowTaskStatePlugin):");
        mappings.AppendLine("- SetRequirementValueAsync(fieldName, value)");
        mappings.AppendLine("- AppendRequirementValueAsync(fieldName, value)");
        mappings.AppendLine("- ClearRequirementValueAsync(fieldName)");
        mappings.AppendLine("- SetRequirementStatusAsync(fieldName, isComplete, reason)");
        mappings.AppendLine("- GetRequirementValueAsync(fieldName)");
        mappings.AppendLine("- GetRequirementStatusAsync(fieldName)");
        mappings.AppendLine();

        if (template.Sections == null)
        {
            mappings.AppendLine("No sections defined on the template.");
            return mappings.ToString();
        }

        foreach (var section in template.Sections.OrderBy(s => s.SortOrder))
        {
            mappings.AppendLine($"Section: {section.DisplayName} ({section.Name})");
            if (section.Requirements == null)
            {
                mappings.AppendLine("  No requirements defined.");
                mappings.AppendLine();
                continue;
            }

            foreach (var requirement in section.Requirements.OrderBy(r => r.SortOrder))
            {
                mappings.AppendLine($"  Field: {requirement.FieldName}");
                if (!string.IsNullOrWhiteSpace(requirement.DisplayName))
                {
                    mappings.AppendLine($"  Display: {requirement.DisplayName}");
                }

                if (!string.IsNullOrWhiteSpace(requirement.Description))
                {
                    mappings.AppendLine($"  Description: {requirement.Description}");
                }

                mappings.AppendLine($"  Required: {requirement.IsRequired}");
                mappings.AppendLine();
            }
        }

        return mappings.ToString();
    }

    /// <summary>
    /// Builds a quick reference guide listing all required fields for validation.
    /// </summary>
    /// <param name="template">The Flow Task template.</param>
    /// <returns>A formatted list of required fields.</returns>
    public static string BuildRequiredFieldsReference(FlowTaskTemplateDetailDto? template)
    {
        if (template == null || template.Sections == null)
        {
            return "No required fields defined.";
        }

        var requiredFields = template.Sections
            .SelectMany(s => s.Requirements ?? new List<FlowTaskRequirementDto>())
            .Where(r => r.IsRequired)
            .OrderBy(r => r.SortOrder)
            .ToList();

        var reference = new StringBuilder();
        reference.AppendLine("Required fields for this template:");
        reference.AppendLine();

        if (!requiredFields.Any())
        {
            reference.AppendLine("No required fields for this template.");
            return reference.ToString();
        }

        foreach (var requirement in requiredFields)
        {
            reference.AppendLine($"- **{requirement.FieldName}** ({requirement.DisplayName}): {requirement.Description ?? "No description"}");
        }

        return reference.ToString();
    }
}
