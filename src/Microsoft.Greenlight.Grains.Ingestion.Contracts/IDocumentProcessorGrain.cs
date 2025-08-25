// Copyright (c) Microsoft Corporation. All rights reserved.
using System;
using System.Threading.Tasks;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts
{
    /// <summary>
    /// Result of a document processing operation.
    /// </summary>
    public class DocumentProcessResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public static DocumentProcessResult Ok() => new DocumentProcessResult { Success = true };
        public static DocumentProcessResult Fail(string error) => new DocumentProcessResult { Success = false, Error = error };
    }

    /// <summary>
    /// Grain contract for processing a single document as part of ingestion.
    /// </summary>
    public interface IDocumentProcessorGrain : IGrainWithGuidKey
    {
        [ResponseTimeout("2.00:00:00")] // Long-running: may wait for cluster-wide lease
        Task<DocumentProcessResult> ProcessDocumentAsync(Guid documentId);
    }
}