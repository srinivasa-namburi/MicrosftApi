namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// A DomainGroupInfo is a collection of document processes that together can be exposed
    /// as a single endpoint for CoPilot users to interact with. 
    /// </summary>
    public class DomainGroupInfo
    {
        /// <summary>
        /// Unique identifier of the domain group.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name of the Domain Group
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? Description { get; set; }
        /// <summary>
        /// Associated Document Processes for the Domain Group
        /// </summary>
        public List<DocumentProcessInfo> DocumentProcesses { get; set; } = [];

        /// <summary>
        /// Whether to expose a Copilot Agent endpoint for the Domain Group
        /// </summary>
        public bool ExposeCoPilotAgentEndpoint { get; set; } = false;
        /// <summary>
        /// Whether to require authentication for the Copilot Agent endpoint. Enabling this
        /// will require CoPilot users to be authorized to use the application/endpoint
        /// </summary>
        public bool AuthenticateCoPilotAgentEndpoint { get; set; } = true;
    }
}