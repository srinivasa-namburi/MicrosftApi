// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.V2.Shared.Configuration;

public sealed class NrcProcessingOptions
{
    public const string PropertyName = "NrcFileProcessor";

    public NrcFileProcessingOptions NrcFileProcessing { get; set; } = null!;

    public class NrcFileProcessingOptions
    {
        /// <summary>
        /// Set this bool to true in the appsettings file to run the RunNrcFileProcessor functionality
        /// </summary>
        public bool RunNrcFileProcessor { get; set; } = false;
        public string AzureStorageConnectionString { get; set; } = string.Empty;
        public string UploadContainerName { get; set; } = string.Empty;
        public string CsvFileContainerName { get; set; } = string.Empty;
        public string CsvFileName { get; set; } = string.Empty;
    }
}
