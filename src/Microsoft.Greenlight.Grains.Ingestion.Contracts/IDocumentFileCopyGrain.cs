using System.Threading.Tasks;
using System;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts
{
    /// <summary>
    /// Result of a file copy operation.
    /// </summary>
    public class FileCopyResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public static FileCopyResult Ok() => new FileCopyResult { Success = true };
        public static FileCopyResult Fail(string error) => new FileCopyResult { Success = false, Error = error };
    }

    /// <summary>
    /// Grain contract for copying files as part of document ingestion.
    /// </summary>
    public interface IDocumentFileCopyGrain : IGrainWithGuidKey
    {
        Task<FileCopyResult> CopyFileAsync(Guid documentId);
    }
}