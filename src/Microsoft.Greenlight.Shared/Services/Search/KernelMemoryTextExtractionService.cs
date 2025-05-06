using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Greenlight.Shared.Services.Search
{
    /// <summary>
    /// Implementation of IKernelMemoryTextExtractionService using KernelMemory's text extraction
    /// </summary>
    public class KernelMemoryTextExtractionService : IKernelMemoryTextExtractionService
    {
        private readonly ILogger<KernelMemoryTextExtractionService> _logger;
        private readonly IKernelMemoryInstanceFactory _kernelMemoryFactory;

        /// <summary>
        /// Creates a new instance of KernelMemoryTextExtractionService
        /// </summary>
        public KernelMemoryTextExtractionService(
            ILogger<KernelMemoryTextExtractionService> logger,
            IKernelMemoryInstanceFactory kernelMemoryFactory)
        {
            _logger = logger;
            _kernelMemoryFactory = kernelMemoryFactory;
        }

        /// <inheritdoc />
        public async Task<string> ExtractTextFromDocumentAsync(Stream documentStream, string fileName)
        {
            try
            {
                if (documentStream == null || documentStream.Length == 0)
                {
                    _logger.LogWarning("Document stream is null or empty");
                    return string.Empty;
                }

                // Reset stream position if possible
                if (documentStream.CanSeek)
                {
                    documentStream.Position = 0;
                }

                // Get KernelMemory instance for ad hoc uploads
                var memory = _kernelMemoryFactory.GetKernelMemoryForAdhocUploads();

                // Generate a unique document ID for this extraction
                string documentId = $"temp-extraction-{Guid.NewGuid()}";

                // Import the document (just for text extraction)
                await memory.ImportDocumentAsync(
                    documentId: documentId,
                    content: documentStream,
                    fileName: fileName,
                    steps: new[] { "extract" }
                );

                // Wait for extraction to complete - since this is a background process
                var status = await memory.GetDocumentStatusAsync(documentId);
                int attempts = 0;
                const int maxAttempts = 10;

                while (!status.Completed && attempts < maxAttempts)
                {
                    await Task.Delay(500);
                    status = await memory.GetDocumentStatusAsync(documentId);
                    attempts++;
                }

                // Extract text by exporting to a text file
                var streamableFileContent = await memory.ExportFileAsync(documentId, $"{fileName}.extract.txt");
                var fileStreamData = await streamableFileContent.GetStreamAsync();

                // Read the extracted text into a string
                string fileContent;
                using (var reader = new StreamReader(fileStreamData))
                {
                    fileContent = await reader.ReadToEndAsync();
                }

                // Clean up the temporary document
                await memory.DeleteDocumentAsync(documentId);

                return fileContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from document {FileName}", fileName);
                return string.Empty;
            }
        }
    }
}
