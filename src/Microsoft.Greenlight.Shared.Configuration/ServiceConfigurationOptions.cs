namespace Microsoft.Greenlight.Shared.Configuration;

public class ServiceConfigurationOptions
{
    public const string PropertyName = "ServiceConfiguration";

    public AzureMapsOptions AzureMaps { get; set; } = new AzureMapsOptions();
    public OpenAiOptions OpenAi { get; set; } = new OpenAiOptions();
    public CognitiveSearchOptions CognitiveSearch { get; set; } = new CognitiveSearchOptions();
    public DocumentIntelligenceOptions DocumentIntelligence { get; set; } = new DocumentIntelligenceOptions();
    public GreenlightServicesOptions GreenlightServices { get; set; } = new GreenlightServicesOptions();
    public SQLOptions SQL { get; set; } = new SQLOptions(); 

    public class AzureMapsOptions
    {
        public string? Key { get; set; } = string.Empty;
    }
    
    public class GreenlightServicesOptions
    {
        public FeatureFlagsOptions FeatureFlags { get; set; } = new FeatureFlagsOptions(); 
        public DocumentGenerationOptions DocumentGeneration { get; set; } = new DocumentGenerationOptions();
        public DocumentIngestionOptions DocumentIngestion { get; set; } = new DocumentIngestionOptions();
        public List<DocumentProcessOptions?> DocumentProcesses { get; set; } = new List<DocumentProcessOptions?>();

        public class FeatureFlagsOptions
        {
            public bool EnableMassDocumentProduction { get; set; }
            public bool EnableReviews { get; set; }
        }

        public class DocumentIngestionOptions
        {
            public ushort NumberOfIngestionWorkers { get; set; }
            public bool ProcessTables { get; set; }
            public bool ScheduledIngestion { get; set; }
        }

        public class DocumentGenerationOptions
        {
            public bool DurableDevelopmentServices { get; set; }
            public bool CreateBodyTextNodes { get; set; }
            public bool UseFullDocumentOutlineGeneration { get; set; } = false;
            public ushort NumberOfGenerationWorkers { get; set; }

        }
    }

    public class SQLOptions
    {
        public string DatabaseName { get; set; } = string.Empty;
    }

    public class OpenAiOptions
    {
        public string GPT4128KModelDeploymentName { get; set; } = string.Empty;
        public string GPT432KModelDeploymentName { get; set; } = string.Empty;
        public string GPT4oModelDeploymentName { get; set; } = string.Empty;

        public string Gpt4o_Or_Gpt4128KDeploymentName
        {
            get
            {
                return string.IsNullOrWhiteSpace(GPT4oModelDeploymentName)
                    ? GPT4128KModelDeploymentName
                    : GPT4oModelDeploymentName;
            }
        }

        public string EmbeddingModelDeploymentName { get; set; } = string.Empty;
    }

    public class CognitiveSearchOptions
    {
        public string SemanticSearchConfigName { get; set; } = string.Empty;
        public string VectorSearchProfileName { get; set; } = string.Empty;
        public string VectorSearchHnswConfigName { get; set; } = string.Empty;
    }

    public class DocumentIntelligenceOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
    }
}
