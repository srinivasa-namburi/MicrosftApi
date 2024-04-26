namespace ProjectVico.V2.Shared.Configuration;

public class ServiceConfigurationOptions
{
    public const string PropertyName = "ServiceConfiguration";

    public AzureMapsOptions AzureMaps { get; set; } = new AzureMapsOptions();
    public OpenAiOptions OpenAi { get; set; } = new OpenAiOptions();
    public CognitiveSearchOptions CognitiveSearch { get; set; } = new CognitiveSearchOptions();
    public DocumentIntelligenceOptions DocumentIntelligence { get; set; } = new DocumentIntelligenceOptions();
    public ProjectVicoServicesOptions ProjectVicoServices { get; set; } = new ProjectVicoServicesOptions();
    public SQLOptions SQL { get; set; } = new SQLOptions(); 

    public class AzureMapsOptions
    {
        public string? Key { get; set; } = string.Empty;
    }
    
    public class ProjectVicoServicesOptions
    {
        public DocumentGenerationOptions DocumentGeneration { get; set; } = new DocumentGenerationOptions();
        public DocumentIngestionOptions DocumentIngestion { get; set; } = new DocumentIngestionOptions();
        public List<DocumentProcessOptions?> DocumentProcesses { get; set; } = new List<DocumentProcessOptions?>();
       
        public class DocumentIngestionOptions
        {
            public ushort NumberOfIngestionWorkers { get; set; }
            public bool ProcessTables { get; set; }
        }

        public class DocumentGenerationOptions
        {
            public bool DurableDevelopmentServices { get; set; }
            public bool CreateBodyTextNodes { get; set; }
            public bool UseFullDocumentOutlineGeneration { get; set; }
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