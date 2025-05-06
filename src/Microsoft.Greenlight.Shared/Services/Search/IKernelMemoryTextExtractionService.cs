namespace Microsoft.Greenlight.Shared.Services.Search
{
    /// <summary>
    /// Service for extracting text from documents for use in Kernel Memory
    /// </summary>
    public interface IKernelMemoryTextExtractionService
    {
        /// <summary>
        /// Extracts text from a document stream
        /// </summary>
        /// <param name="documentStream">The document content as a stream</param>
        /// <param name="fileName">The name of the file</param>
        /// <returns>The extracted text</returns>
        Task<string> ExtractTextFromDocumentAsync(Stream documentStream, string fileName);
    }
}