// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Web.DocGen.Client.Components.Configuration
{
    /// <summary>
    /// Represents the different configuration sections available in the application.
    /// </summary>
    public enum ConfigSection
    {
        /// <summary>
        /// Frontend configuration section for site branding and UI settings.
        /// </summary>
        Frontend,

        /// <summary>
        /// Features configuration section for feature flags and toggles.
        /// </summary>
        Features,

        /// <summary>
        /// AI Models configuration section for model configurations and endpoints.
        /// </summary>
        AiModels,

        /// <summary>
        /// Scalability configuration section for worker counts and performance settings.
        /// </summary>
        Scalability,

        /// <summary>
        /// Vector Store configuration section for embedding and search settings.
        /// </summary>
        VectorStore,

        /// <summary>
        /// OCR configuration section for text extraction settings.
        /// </summary>
        Ocr,

        /// <summary>
        /// Secrets configuration section for API keys and credentials.
        /// </summary>
        Secrets,

        /// <summary>
        /// File Storage configuration section for storage configuration and providers.
        /// </summary>
        FileStorage,

        /// <summary>
        /// Host Names configuration section for domain and URL configuration.
        /// </summary>
        HostNames,

        /// <summary>
        /// MCP Server configuration section for Model Context Protocol configuration.
        /// </summary>
        Mcp,

        /// <summary>
        /// Flow configuration section for Flow AI Assistant settings and model configuration.
        /// </summary>
        Flow
    }
}