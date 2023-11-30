// Copyright (c) Microsoft. All rights reserved.

using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;

namespace ProjectVico.Backend.DocumentIngestion.Shared;

/// <summary>
/// A class with functionality to cache NRC Licencing PDF Documents from the NRC website to Azure Blob Storage
/// </summary>
public class NrcFileProcessor : INrcFileProcessor
{
    // A field to store the base URL for NRC files
    private const string NrcBaseUrl = "https://www.nrc.gov/docs/";

    // A field to store the blob service client
    private readonly BlobServiceClient _blobServiceClient;
    private readonly NrcProcessingOptions s_nrcProcessingOptions = new NrcProcessingOptions();
    private readonly string csvBlobStorageContainerName;
    private readonly string connectionString;
    private readonly string uploadContainerName;

    // A constructor that takes a configuration object
    public NrcFileProcessor(IOptions<NrcProcessingOptions> nrcProcessingOptions)
    {
        this.s_nrcProcessingOptions = nrcProcessingOptions.Value;

        // Get the Aure Storage connection string from the configuration
        this.connectionString = this.s_nrcProcessingOptions.NrcFileProcessing.AzureStorageConnectionString;
        this.csvBlobStorageContainerName = this.s_nrcProcessingOptions.NrcFileProcessing.CsvFileContainerName;
        this.uploadContainerName = this.s_nrcProcessingOptions.NrcFileProcessing.UploadContainerName;
    }

    /// <summary>
    /// A method which (1) takes a container and file name of a CSV file with information on a list of NRC PDFs to be cached,
    /// in the Azure Blob Storage account pointed to in the settings file,
    /// (2) runs through that CSV file to download each of the PDF files with valid "AccessionNumber"s in the CSV
    /// and uploads these files to the Blob Storage cache with the container name specified in the settings file.
    /// Note, this method assumes the CSV points to valid NRC documents, and that the "AccessionNumber" is the 14th column in that CSV file.
    /// </summary>
    /// <param name="csvFileName"></param>
    /// <param name="csvContainerName"></param>
    /// <returns></returns>
    public async Task ProcessNrcFilesAsync(string csvFileName, string csvContainerName)
    {
        var csvStream = await this.OpenMemoryStreamToBlobFile(csvFileName, csvContainerName);

        // Reset the stream position to the beginning
        csvStream.Position = 0;

        // Open the CSV file for reading
        using var reader = new StreamReader(csvStream);

        // Read the first line (header) and ignore it
        reader.ReadLine();

        // Read the rest of the lines
        while (!reader.EndOfStream)
        {
            try
            {
                // Read a line and split it by comma
                var line = reader.ReadLine();
                var values = line.Split('~');

                // Get the accession number from the 14th column
                var accessionNumber = values[13];

                // Construct the URL for the NRC file
                var nrcFileUrl = NrcBaseUrl + accessionNumber;

                string filename = await this.DownloadFile(nrcFileUrl);

                // Upload the NRC file to the blob storage
                await this.UploadFileToBlobAsync(filename, filename);
            }
            catch (Exception e)
            {
                Console.WriteLine($"ProcessNrcFilesAsync: Exception caught while running through a line of the CSV file: {e}");
            }
        }
    }

    /// <summary>
    /// Open a memory stream to a blob file, for example a CSV, to allow iterative processing
    /// </summary>
    /// <param name="blobFileName"></param>
    /// <param name="blobContainerName"></param>
    /// <returns></returns>
    private async Task<MemoryStream> OpenMemoryStreamToBlobFile(string blobFileName, string blobContainerName)
    {
        var csvStream = new MemoryStream();

        try
        {
            // Create a blob service client from the connection string
            var _blobServiceClient = new BlobServiceClient(this.connectionString);

            // Create a blob container client for the specified container name
            var csvContainerClient = _blobServiceClient.GetBlobContainerClient(blobContainerName);

            // Create a blob client for the specified CSV file name
            var csvBlobClient = csvContainerClient.GetBlobClient(blobFileName);

            // Download the CSV file to a memory stream
            await csvBlobClient.DownloadToAsync(csvStream);
        }
        catch (Exception e)
        {
            Console.WriteLine($"OpenMemoryStreamToBlobFile: Exception caught opening memory stream to the file {blobFileName} in container {blobContainerName}. Double check the settings for the NrcFileProcessor functionality. Exception: {e}");
            throw e;
        }

        return csvStream;
    }

    /// <summary>
    /// Upload the specified file to Azure blob storage into the container specified in the settings of this NrcFileProcessor object
    /// </summary>
    /// <param name="filePath">File to Upload</param>
    /// <param name="blobName">The name to five the file being uploaded</param>
    /// <returns></returns>
    public async Task UploadFileToBlobAsync(string filePath, string blobName)
    {
        try
        {
            // Create a BlobServiceClient using the connection string
            var blobServiceClient = new BlobServiceClient(this.connectionString);

            // Get a reference to the container
            var containerClient = blobServiceClient.GetBlobContainerClient(this.uploadContainerName);

            // Create the container if it does not exist
            await containerClient.CreateIfNotExistsAsync();

            // Get a reference to the blob
            var blobClient = containerClient.GetBlobClient(blobName);

            // Open the file and upload its data
            using FileStream uploadFileStream = File.OpenRead(filePath);
            await blobClient.UploadAsync(uploadFileStream, new BlobHttpHeaders { ContentType = "application/octet-stream" });
            Console.WriteLine($"NrcFileProcessor: PDF file cached successfully to Blob Storage : {blobName}");
            uploadFileStream.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine($"UploadFileToBlobAsync: Exception caught uploading file to Azure blob storage from filepath: {filePath} and blobname {blobName}. Exception: {e}");
            throw e;
        }
    }

    /// <summary>
    /// Download the file from the given url in a way that immitates a web browser downloading that file
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public async Task<string> DownloadFile(string url)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false, // Handle redirects manually
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            using var client = new HttpClient(handler);

            // Add headers found using browser developer tools
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");

            try
            {
                var response = await client.GetAsync(url); // For example "https://www.nrc.gov/docs/ML032731616"
                string pdfFileName = null;

                // Check for redirect
                if (response.StatusCode == HttpStatusCode.Redirect ||
                    response.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    var newLocation = response.Headers.Location;


                    // Now try to get the content from the new location
                    var pdfResponse = await client.GetAsync(newLocation);
                    pdfFileName = newLocation.Segments.Last();
                    if (pdfResponse.IsSuccessStatusCode)
                    {
                        var content = await pdfResponse.Content.ReadAsByteArrayAsync();
                        await System.IO.File.WriteAllBytesAsync(pdfFileName, content);
                        Console.WriteLine($"NrcFileProcessor: PDF file downloaded successfully : {pdfFileName}");
                    }
                    else
                    {
                        Console.WriteLine($"NrcFileProcessor: Failed to download PDF: {pdfResponse.StatusCode}");
                    }
                }
                else
                {
                    Console.WriteLine($"NrcFileProcessor: Initial request failed: {response.StatusCode}");
                }

                return pdfFileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"DownloadFile: Exception caught downloading file from url: {url}. Exception: {e}");
            throw e;
        }
        return null;
    }
}
