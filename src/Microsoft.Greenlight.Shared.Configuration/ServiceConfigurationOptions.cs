// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel.DataAnnotations;

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
    /// Host name override options for web and API endpoints.
    /// </summary>
    public HostNameOverrideOptions HostNameOverride { get; set; } = new HostNameOverrideOptions();

    /// <summary>
    /// If set, these options override the host names for the Web DocGen and Greenlight API sites.
    /// Used among other things for third party layer 7 load balancers.
    /// </summary>
    public class HostNameOverrideOptions
    {
        /// <summary>
        /// An overridden host name for the Web DocGen frontend site.
        /// Use only the host name, no https or trailing slash
        /// </summary>
        public string Web { get; set; } = string.Empty;

        /// <summary>
        /// An overridden host name for the Greenlight API site.
        /// Use only the host name, no https or trailing slash
        /// </summary>
        public string Api { get; set; } = string.Empty;

        /// <summary>
        /// An overriden host name for the Azure SignalR service
        /// Use only the host name, no https or trailing slash
        /// </summary>
        public string SignalR { get; set; } = string.Empty;


    }

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
        /// Global options for various settings
        /// </summary>
        public GlobalOptions Global { get; set; } = new GlobalOptions();

        /// <inheritdoc cref="ScalabilityOptions"/>>
        public ScalabilityOptions Scalability { get; set; } = new ScalabilityOptions();

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
        /// Global vector store configuration options (Semantic Kernel vector store layer).
        /// Bound under ServiceConfiguration:GreenlightServices:VectorStore.
        /// </summary>
        public VectorStoreOptions VectorStore { get; set; } = new VectorStoreOptions();

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
        /// Options for scaling the system
        /// </summary>
        public class ScalabilityOptions
        {
            /// <summary>
            /// Number of available validation workers
            /// </summary>
            public int NumberOfValidationWorkers { get; set; } = 1;
            /// <summary>
            /// Number of available document generation workers
            /// </summary>
            public int NumberOfGenerationWorkers { get; set; } = 1;
            /// <summary>
            /// Number of available document ingestion workers
            /// </summary>
            public int NumberOfIngestionWorkers { get; set; } = 1;

            /// <summary>
            /// Number of available document review workers
            /// </summary>
            public int NumberOfReviewWorkers { get; set; } = 1;
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

            /// <summary>
            /// Enable External Data View feature in the UI. Off by default.
            /// </summary>
            public bool EnableExternalDataView { get; set; }
        }

        /// <summary>
        /// Options for document ingestion.
        /// </summary>
        public class DocumentIngestionOptions
        {
            /// <summary>
            /// Enable processing tables.
            /// </summary>
            public bool ProcessTables { get; set; }

            /// <summary>
            /// Enable a scheduled ingestion.
            /// </summary>
            public bool ScheduledIngestion { get; set; }

            /// <summary>
            /// Enable local file storage as an available option for new file storage sources.
            /// When false, users cannot create new local file storage sources, but existing ones remain functional.
            /// </summary>
            public bool LocalFileStorageAvailable { get; set; } = false;

            /// <summary>
            /// OCR configuration for ingestion (languages, sources, caching).
            /// </summary>
            public OcrOptions Ocr { get; set; } = new OcrOptions();

            /// <summary>
            /// OCR options for PDF/image text extraction.
            /// </summary>
            public class OcrOptions
            {
                /// <summary>
                /// Default OCR languages (Tesseract codes) to use, combined like "eng+jpn".
                /// </summary>
                public List<string> DefaultLanguages { get; set; } = new List<string> { "eng" };

                /// <summary>
                /// Blob container name where optional *.traineddata files are stored.
                /// </summary>
                public string TessdataBlobContainer { get; set; } = "ocr-tessdata";

                /// <summary>
                /// Allow downloading missing language files from external repository (GitHub tessdata).
                /// </summary>
                public bool AllowExternalDownloads { get; set; }

                /// <summary>
                /// Base URL for external language downloads (raw file URL). Example: https://github.com/tesseract-ocr/tessdata/raw/main
                /// </summary>
                public string ExternalRepoBaseUrl { get; set; } = "https://github.com/tesseract-ocr/tessdata/raw/main";
            }
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
            /// Cache duration (seconds) for document generation status endpoints.
            /// </summary>
            public int StatusCacheSeconds { get; set; } = 30;

        }

        /// <summary>
        /// Global system-level feature toggles and infrastructure usage flags.
        /// </summary>
        public class GlobalOptions
        {
            /// <summary>
            /// Use Application Insights to generate trace and performance data
            /// </summary>
            public bool UseApplicationInsights { get; set; }

            /// <summary>
            /// Use Azure SQL Server for local development instead of a container
            /// </summary>
            public bool UseAzureSqlServer { get; set; }

            /// <summary>
            /// Use Postgres for Kernel Memory MemoryDB storage
            /// </summary>
            public bool UsePostgresMemory { get; set; }

            /// <summary>
            /// Enables the heavy Vector Store ID Fix job that migrates old filename-based IDs
            /// to canonical Base64Url-encoded IDs. Disabled by default. When disabled, the job will
            /// not run on startup and the monthly reminder will not be scheduled.
            /// </summary>
            public bool EnableVectorStoreIdFixJob { get; set; } = false;
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
