// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Effort level supplied to reasoning-capable Azure OpenAI models.
/// Moved here so all consumers share the same enum.
/// </summary>
public enum ChatReasoningEffortLevel
{
    /// <summary>Minimal reasoning effort.</summary>
    Minimal,
    /// <summary>Low reasoning effort.</summary>
    Low,
    /// <summary>Medium reasoning effort.</summary>
    Medium,
    /// <summary>High reasoning effort.</summary>
    High
}
