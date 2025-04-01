namespace Microsoft.Greenlight.Shared.Configuration;

/// <summary>
/// Options for configuring various services.
/// </summary>
public class ServiceConfigurationOptions
{
    /// <summary> 
    /// Property name for the service configuration options.
    /// </summary>
    public const string PropertyName = "ServiceConfiguration";

    /// <summary>
    /// Options for Azure Maps service.
    /// </summary>
    public AzureMapsOptions AzureMaps { get; set; } = new AzureMapsOptions();

    /// <summary>
    /// Options for OpenAI service.
    /// </summary>
    public OpenAiOptions OpenAi { get; set; } = new OpenAiOptions();

    /// <summary>
    /// Options for Cognitive Search service.
    /// </summary>
    public CognitiveSearchOptions CognitiveSearch { get; set; } = new CognitiveSearchOptions();

    /// <summary>
    /// Options for Document Intelligence service.
    /// </summary>
    public DocumentIntelligenceOptions DocumentIntelligence { get; set; } = new DocumentIntelligenceOptions();

    /// <summary>
    /// Options for Greenlight services.
    /// </summary>
    public GreenlightServicesOptions GreenlightServices { get; set; } = new GreenlightServicesOptions();

    /// <summary>
    /// Options for SQL service.
    /// </summary>
    public SQLOptions SQL { get; set; } = new SQLOptions();

    /// <summary>
    /// Options for Azure Maps service.
    /// </summary>
    public class AzureMapsOptions
    {
        /// <summary>
        /// The API key for Azure Maps.
        /// </summary>
        public string? Key { get; set; } = string.Empty;
    }

    /// <summary>
    /// Options for Greenlight services.
    /// </summary>
    public class GreenlightServicesOptions
    {
        
        /// <inheritdoc cref="FrontendOptions"/>>
        public FrontendOptions FrontEnd { get; set; } = new FrontendOptions();

        /// <summary>
        /// Options for feature flags.
        /// </summary>
        public FeatureFlagsOptions FeatureFlags { get; set; } = new FeatureFlagsOptions();

        /// <summary>
        /// Options for document generation.
        /// </summary>
        public DocumentGenerationOptions DocumentGeneration { get; set; } = new DocumentGenerationOptions();

        /// <summary>
        /// Options for document ingestion.
        /// </summary>
        public DocumentIngestionOptions DocumentIngestion { get; set; } = new DocumentIngestionOptions();

        /// <summary>
        /// Options for document processes.
        /// </summary>
        public List<DocumentProcessOptions?> DocumentProcesses { get; set; } = new List<DocumentProcessOptions?>();

        /// <summary>
        /// Reference indexing options.
        /// </summary>
        public ReferenceIndexingOptions ReferenceIndexing { get; set; } = new ReferenceIndexingOptions();

        /// <summary>
        /// Options for reference indexing.
        /// </summary>
        public class ReferenceIndexingOptions
        {
            /// <summary>
            /// Number of minutes between each scheduled refresh of the reference cache.
            /// </summary>
            public int RefreshIntervalMinutes { get; set; }
        }


        /// <summary>
        /// Options for frontend - these are typically used for display manipulation
        /// </summary>
        public class FrontendOptions
        {
            /// <summary>
            /// The name of the site/application as displayed in the user interface.
            /// </summary>
            public string SiteName { get; set; } = "Generative AI for Permitting";
        }

        /// <summary>
        /// Options for feature flags.
        /// </summary>
        public class FeatureFlagsOptions
        {
            /// <summary>
            /// Enable mass document production.
            /// </summary>
            public bool EnableMassDocumentProduction { get; set; }

            /// <summary>
            /// Enable reviews.
            /// </summary>
            public bool EnableReviews { get; set; }

            /// <summary>
            /// Enable reference frontend.
            /// </summary>
            public bool EnableReferenceFrontend { get; set; }

            /// <summary>
            /// Enable content reference system.
            /// </summary>
            public bool EnableContentReferences { get; set; }
        }

        /// <summary>
        /// Options for document ingestion.
        /// </summary>
        public class DocumentIngestionOptions
        {
            /// <summary>
            /// Number of ingestion workers.
            /// </summary>
            public ushort NumberOfIngestionWorkers { get; set; }

            /// <summary>
            /// Enable processing tables.
            /// </summary>
            public bool ProcessTables { get; set; }

            /// <summary>
            /// Enable a scheduled ingestion.
            /// </summary>
            public bool ScheduledIngestion { get; set; }
        }

        /// <summary>
        /// Options for document generation.
        /// </summary>
        public class DocumentGenerationOptions
        {
            /// <summary>
            /// Enable durable development services.
            /// </summary>
            public bool DurableDevelopmentServices { get; set; }

            /// <summary>
            /// Enable creating text nodes.
            /// </summary>
            public bool CreateBodyTextNodes { get; set; }

            /// <summary>
            /// Enable full document outline generation.
            /// </summary>
            public bool UseFullDocumentOutlineGeneration { get; set; } = false;

            /// <summary>
            /// Number of generation workers.
            /// </summary>
            public ushort NumberOfGenerationWorkers { get; set; }
        }
    }

    /// <summary>
    /// Options for SQL service.
    /// </summary>
    public class SQLOptions
    {
        /// <summary>
        /// The name of the database.
        /// </summary>
        public string DatabaseName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Options for OpenAI service.
    /// </summary>
    public class OpenAiOptions
    {
        /// <summary>
        /// The deployment name for GPT-4 128K model.
        /// </summary>
        public string GPT4128KModelDeploymentName { get; set; } = string.Empty;

        /// <summary>
        /// The deployment name for GPT-4 32K model.
        /// </summary>
        public string GPT432KModelDeploymentName { get; set; } = string.Empty;

        /// <summary>
        /// The deployment name for GPT-4o model.
        /// </summary>
        public string GPT4oModelDeploymentName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the deployment name for GPT-4o or GPT-4 128K model.
        /// </summary>
        public string Gpt4o_Or_Gpt4128KDeploymentName
        {
            get
            {
                return string.IsNullOrWhiteSpace(GPT4oModelDeploymentName)
                    ? GPT4128KModelDeploymentName
                    : GPT4oModelDeploymentName;
            }
        }

        /// <summary>
        /// Gets the deployment name for the o3-mini model.
        /// </summary>
        public string O3MiniModelDeploymentName { get; set; } = string.Empty;

        /// <summary>
        /// The deployment name for embedding model.
        /// </summary>
        public string EmbeddingModelDeploymentName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Options for Cognitive Search service.
    /// </summary>
    public class CognitiveSearchOptions
    {
        /// <summary>
        /// The name of the semantic search configuration.
        /// </summary>
        public string SemanticSearchConfigName { get; set; } = string.Empty;

        /// <summary>
        /// The name of the vector search profile.
        /// </summary>
        public string VectorSearchProfileName { get; set; } = string.Empty;

        /// <summary>
        /// The name of the vector search HNSW configuration.
        /// </summary>
        public string VectorSearchHnswConfigName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Options for Document Intelligence service.
    /// </summary>
    public class DocumentIntelligenceOptions
    {
        /// <summary>
        /// The endpoint for the service.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// The API key for the service.
        /// </summary>
        public string Key { get; set; } = string.Empty;
    }
}
