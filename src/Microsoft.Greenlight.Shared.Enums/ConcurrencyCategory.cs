// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Defines known concurrency categories that map to Greenlight Services scalability settings.
/// </summary>
public enum ConcurrencyCategory
{
    /// <summary>
    /// Document validation concurrency. Backed by NumberOfValidationWorkers.
    /// </summary>
    Validation = 0,

    /// <summary>
    /// Document generation concurrency. Backed by NumberOfGenerationWorkers.
    /// </summary>
    Generation = 1,

    /// <summary>
    /// Document ingestion concurrency. Backed by NumberOfIngestionWorkers.
    /// </summary>
    Ingestion = 2,

    /// <summary>
    /// Document review concurrency. Backed by NumberOfReviewWorkers.
    /// </summary>
    Review = 3
}

