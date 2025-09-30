using System;
using System.Linq;

namespace Microsoft.Greenlight.Shared.Helpers;

/// <summary>
/// Helper for parsing Azure OpenAI connection strings in various formats.
/// </summary>
public static class AzureOpenAIConnectionStringParser
{
    /// <summary>
    /// Represents the parsed Azure OpenAI connection string.
    /// </summary>
    public record AzureOpenAIConnectionInfo(string Endpoint, string? Key);

    /// <summary>
    /// Parses an Azure OpenAI connection string supporting multiple formats.
    /// </summary>
    /// <param name="connectionString">The connection string to parse</param>
    /// <returns>Parsed connection information</returns>
    /// <exception cref="ArgumentException">Thrown when the connection string is invalid</exception>
    public static AzureOpenAIConnectionInfo Parse(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

        string endpoint;
        string? key = null;

        // Support multiple formats:
        // 1. "Endpoint=https://example.openai.azure.com/;Key=abc123" (with key)
        // 2. "Endpoint=https://example.openai.azure.com/;Key=" (no key)
        // 3. "Endpoint=https://example.openai.azure.com/" (no key parameter)
        // 4. "https://example.openai.azure.com/" (just URL, no key)
        if (connectionString.IndexOf("Endpoint=", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Parse structured format
            var parts = connectionString.Split(';');
            var endpointPart = parts.FirstOrDefault(p => p.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase));
            var keyPart = parts.FirstOrDefault(p => p.StartsWith("Key=", StringComparison.OrdinalIgnoreCase));
            
            if (endpointPart == null)
                throw new ArgumentException("Invalid connection string: missing Endpoint", nameof(connectionString));
            
            endpoint = endpointPart.Substring("Endpoint=".Length);
            if (keyPart != null)
            {
                key = keyPart.Substring("Key=".Length);
                if (string.IsNullOrEmpty(key))
                    key = null; // Treat empty key as no key
            }
        }
        else
        {
            // Simple URL format
            endpoint = connectionString;
        }

        // Validate endpoint URL
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out _))
            throw new ArgumentException($"Invalid endpoint URL in connection string: {endpoint}", nameof(connectionString));

        // Ensure endpoint ends with trailing slash
        if (!endpoint.EndsWith("/"))
            endpoint += "/";

        return new AzureOpenAIConnectionInfo(endpoint, key);
    }
}