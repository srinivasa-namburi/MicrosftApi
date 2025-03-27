namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Defines the different validation execution types available
    /// </summary>
    public enum ValidationPipelineExecutionType
    {
        /// <summary>
        /// Renders the full document as text and runs validation against this original text in parallel for each section. Doesn't
        /// update the original full text with results of each section until full parallel execution has ended.
        /// </summary>
        ParallelFullDocument = 100,
        /// <summary>
        /// Renders the full document as text and runs validation sequentially for each section in ordered fashion, updating
        /// the full document text  after each sequence. This is slow, but thorough.
        /// </summary>
        SequentialFullDocument = 200,
        /// <summary>
        /// Renders the full document as text and runs validation in parallel for each outer chapter, validating each section
        /// inside the chapter in sequence. The text of each full chapter is updated at the end of the parallel section run. Works well
        /// as a first step before running ParallelFullDocument
        /// </summary>
        ParallelByOuterChapter = 300,

    }
}