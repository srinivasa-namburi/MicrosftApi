// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Plugins.DocQnA.Options;

public sealed class AiSettings
{
    public const string PropertyName = "AI";

    public CognitiveSearchOptions CognitiveSearch { get; set; } = null!;
    public OpenAiOptions OpenAI { get; set; } = null!;

    public class OpenAiOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string CompletionModel { get; set; } = string.Empty;
        public string EmbeddingModel { get; set; } = string.Empty;
    }

    public class CognitiveSearchOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Index { get; set; } = string.Empty;
    }

}
