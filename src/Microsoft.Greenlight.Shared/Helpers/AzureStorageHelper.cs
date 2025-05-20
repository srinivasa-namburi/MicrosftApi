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
        else
        {
            // For prod, use the connection string directly (let BlobServiceClient parse it)
            return (new Uri(""), null);
        }
    }

    public static (Uri endpoint, TableSharedKeyCredential? sharedKeyCredential) ParseTableEndpointAndCredential(string connectionString)
    {
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
        else
        {
            return (new Uri(""), null);
        }
    }
}
