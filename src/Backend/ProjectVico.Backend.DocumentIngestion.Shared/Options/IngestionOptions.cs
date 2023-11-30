// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Backend.DocumentIngestion.Shared.Options;

public sealed class IngestionOptions
{
    public const string PropertyName = "Ingestion";
    public string BlobStorageAccountName { get; set; } = string.Empty;
    public string BlobStorageAccountKey { get; set; } = string.Empty;
    public string BlobStorageContainerName { get; set; } = string.Empty;
    public string BlobStorageConnectionString { get; set; } = string.Empty;
    public bool PerformClassification { get; set; } = false;
    public string ClassificationModelName { get; set; } = "nrc-classifier";
}
