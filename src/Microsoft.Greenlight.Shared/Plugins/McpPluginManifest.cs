// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Represents an MCP plugin manifest.
    /// </summary>
    public class McpPluginManifest
    {
        /// <summary>
        /// Gets or sets the name of the MCP plugin.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the MCP plugin.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command to execute the MCP server.
        /// </summary>
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the arguments to pass to the MCP server command.
        /// </summary>
        [JsonPropertyName("arguments")]
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the environment variables to apply when running the command.
        /// Key is the environment variable name, value is the environment variable value.
        /// </summary>
        [JsonPropertyName("environmentVariables")]
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the URL for SSE/HTTP plugins. Null for non-SSE plugins.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the authentication type for the plugin. Null for non-SSE plugins.
        /// </summary>
        [JsonPropertyName("authenticationType")]
        public McpPluginAuthenticationType? AuthenticationType { get; set; }
    }
}