// Copyright (c) Microsoft Corporation. All rights reserved.
using System;
using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic
{
    /// <summary>
    /// Represents the configuration for an agent in a multi-agent system.
    /// Defines the agent's role, instructions, and which plugins it has access to.
    /// </summary>
    public class AgentConfiguration
    {
        /// <summary>
        /// Gets or sets the unique name of the agent.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a description of the agent's role and purpose.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the instructions to provide to the agent.
        /// </summary>
        public string Instructions { get; set; }

        /// <summary>
        /// Gets or sets the names of plugins this agent is allowed to use.
        /// If null or empty, the agent has access to all plugins.
        /// </summary>
        public List<string> AllowedPlugins { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets whether this agent is authorized to terminate the conversation.
        /// </summary>
        public bool HasTerminationAuthority { get; set; } = false;

        /// <summary>
        /// Gets or sets whether this agent is an orchestrator that manages the conversation flow.
        /// </summary>
        public bool IsOrchestrator { get; set; } = false;

        /// <summary>
        /// Gets or sets a callback function to generate custom instructions for this agent.
        /// </summary>
        public Func<string, string, string, string> InstructionsBuilder { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentConfiguration"/> class.
        /// </summary>
        public AgentConfiguration()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentConfiguration"/> class with specified properties.
        /// </summary>
        /// <param name="name">Agent name.</param>
        /// <param name="description">Agent description.</param>
        /// <param name="allowedPlugins">List of plugins this agent can access.</param>
        /// <param name="isOrchestrator">Whether this agent is an orchestrator.</param>
        /// <param name="hasTerminationAuthority">Whether this agent can terminate the conversation.</param>
        public AgentConfiguration(
            string name, 
            string description, 
            List<string> allowedPlugins = null, 
            bool isOrchestrator = false,
            bool hasTerminationAuthority = false)
        {
            Name = name;
            Description = description;
            AllowedPlugins = allowedPlugins ?? new List<string>();
            IsOrchestrator = isOrchestrator;
            HasTerminationAuthority = hasTerminationAuthority;
        }
    }
}