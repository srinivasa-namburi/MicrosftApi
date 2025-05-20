// Copyright (c) Microsoft Corporation. All rights reserved.
using Azure;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Shared.Helpers;

/// <summary>
/// Helper class for Azure Storage connection string parsing and credential selection.
/// </summary>
public static class AzureStorageHelper
{
    /// <summary>
    /// Determines if the connection string is for local development storage.
    /// </summary>
    public static bool IsDevelopmentStorage(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return false;
        return connectionString.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("127.0.0.1")
            || connectionString.Contains("localhost");
    }

    /// <summary>
    /// Extracts the Blob/Table endpoint and StorageSharedKeyCredential for local dev, or returns the original connection string and TokenCredential for prod.
    /// </summary>
    public static (Uri endpoint, StorageSharedKeyCredential? sharedKeyCredential) ParseBlobEndpointAndCredential(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Blob connection string is missing or empty.", nameof(connectionString));

        if (IsDevelopmentStorage(connectionString))
        {
            // For Azurite/local, parse AccountName and AccountKey
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFeqCnf2P=="; // Default Azurite key
            var match = Regex.Match(connectionString, @"AccountKey=([^;]+)");
            if (match.Success)
                accountKey = match.Groups[1].Value;
            var endpoint = new Uri("http://127.0.0.1:10000/devstoreaccount1");
            var endpointMatch = Regex.Match(connectionString, @"BlobEndpoint=([^;]+)");
            if (endpointMatch.Success)
                endpoint = new Uri(endpointMatch.Groups[1].Value);
            return (endpoint, new StorageSharedKeyCredential(accountName, accountKey));
        }
        else if (connectionString.TrimStart().StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            // If the connection string is just a URL, treat it as the endpoint and use default credentials
            return (new Uri(connectionString), null);
        }
        else
        {
            // For prod, use the connection string directly (let BlobServiceClient parse it)
            // Try to extract AccountName, AccountKey, and BlobEndpoint
            var endpointMatch = Regex.Match(connectionString, @"BlobEndpoint=([^;]+)");
            var accountNameMatch = Regex.Match(connectionString, @"AccountName=([^;]+)");
            var accountKeyMatch = Regex.Match(connectionString, @"AccountKey=([^;]+)");
            if (endpointMatch.Success && accountNameMatch.Success && accountKeyMatch.Success)
            {
                var endpoint = new Uri(endpointMatch.Groups[1].Value);
                var accountName = accountNameMatch.Groups[1].Value;
                var accountKey = accountKeyMatch.Groups[1].Value;
                return (endpoint, new StorageSharedKeyCredential(accountName, accountKey));
            }
            // If not, fallback to using the connection string as endpoint (may throw if invalid)
            return (new Uri(connectionString), null);
        }
    }

    public static (Uri endpoint, TableSharedKeyCredential? sharedKeyCredential) ParseTableEndpointAndCredential(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Table connection string is missing or empty.", nameof(connectionString));

        if (IsDevelopmentStorage(connectionString))
        {
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFeqCnf2P==";
            var match = Regex.Match(connectionString, @"AccountKey=([^;]+)");
            if (match.Success)
                accountKey = match.Groups[1].Value;
            var endpoint = new Uri("http://127.0.0.1:10002/devstoreaccount1");
            var endpointMatch = Regex.Match(connectionString, @"TableEndpoint=([^;]+)");
            if (endpointMatch.Success)
                endpoint = new Uri(endpointMatch.Groups[1].Value);
            return (endpoint, new TableSharedKeyCredential(accountName, accountKey));
        }
        else if (connectionString.TrimStart().StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            // If the connection string is just a URL, treat it as the endpoint and use default credentials
            return (new Uri(connectionString), null);
        }
        else
        {
            // For prod, use the connection string directly (let TableServiceClient parse it)
            // Try to extract TableEndpoint, AccountName, and AccountKey
            var endpointMatch = Regex.Match(connectionString, @"TableEndpoint=([^;]+)");
            var accountNameMatch = Regex.Match(connectionString, @"AccountName=([^;]+)");
            var accountKeyMatch = Regex.Match(connectionString, @"AccountKey=([^;]+)");
            if (endpointMatch.Success && accountNameMatch.Success && accountKeyMatch.Success)
            {
                var endpoint = new Uri(endpointMatch.Groups[1].Value);
                var accountName = accountNameMatch.Groups[1].Value;
                var accountKey = accountKeyMatch.Groups[1].Value;
                return (endpoint, new TableSharedKeyCredential(accountName, accountKey));
            }
            // If not, fallback to using the connection string as endpoint (may throw if invalid)
            return (new Uri(connectionString), null);
        }
    }
}
