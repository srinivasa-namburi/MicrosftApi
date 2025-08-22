// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Services.Search.Internal;

/// <summary>
/// Utility for normalizing heterogeneous similarity scores (cosine, distance, etc.) into a 0..1 range.
/// </summary>
internal static class ScoreNormalizer
{
    /// <summary>
    /// Normalizes a raw similarity or distance score into the range [0,1].
    /// Cosine scores in [-1,1] are linearly remapped. Other scores are squashed
    /// with a logistic transform to avoid hard cutoffs.
    /// </summary>
    /// <param name="raw">Raw similarity or distance score.</param>
    /// <returns>Normalized score in the interval [0,1].</returns>
    public static double Normalize(double raw)
    {
        if (double.IsNaN(raw) || double.IsInfinity(raw)) return 0d;
        if (raw >= -1.0001 && raw <= 1.0001)
        {
            return (raw + 1d) / 2d; // Cosine similarity -> [0,1]
        }
        var exp = System.Math.Exp(-raw / 10d);
        var logistic = 1d / (1d + exp);
        if (logistic < 0) return 0;
        if (logistic > 1) return 1;
        return logistic;
    }
}
