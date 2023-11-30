// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Backend.DocumentIngestion.Shared.Options;

public sealed class AiOptions
{
    public const string PropertyName = "AI";

    public OpenAiOptions OpenAI { get; set; } = null!;
    public CognitiveSearchOptions CognitiveSearch { get; set; } = null!;
    public DocumentIntelligenceOptions DocumentIntelligence { get; set; } = null!;
    

    public class OpenAiOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string CompletionModel { get; set; } = string.Empty;
        public string EmbeddingModel { get; set; } = string.Empty;
        public string SummarizationModel { get; set; } = string.Empty;
    }

    public class CognitiveSearchOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string TitleIndex { get; set; } = string.Empty;
        public string SectionIndex { get; set; } = string.Empty;
        public string VectorSearchProfileName { get; set; } = string.Empty;
        public string VectorSearchHnswConfigName { get; set; } = string.Empty;
        public string SemanticSearchConfigName { get; set; } = string.Empty;
        /// <summary>
        /// Kept around for compatability with old code - Specifically the SectionPlugin which requires the format of this index until we can replace it
        /// </summary>
        public string Index { get; set; }
    }

    public class DocumentIntelligenceOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
    }


}
