using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.DomainGroups
{
    /// <summary>
    /// A DomainGroup is a collection of document processes that together can be exposed
    /// as a single endpoint for CoPilot users to interact with. 
    /// </summary>
    public class DomainGroup : EntityBase
    {
        /// <summary>
        /// Name of the Domain Group
        /// </summary>
        public required string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? Description { get; set; }
        /// <summary>
        /// Associated Document Processes for the Domain Group
        /// </summary>
        public List<DynamicDocumentProcessDefinition> DocumentProcesses { get; set; } = [];

        /// <summary>
        /// Whether to expose a Copilot Agent endpoint for the Domain Group
        /// </summary>
        public required bool ExposeCoPilotAgentEndpoint { get; set; } = false;
        /// <summary>
        /// Whether to require authentication for the Copilot Agent endpoint. Enabling this
        /// will require CoPilot users to be authorized to use the application/endpoint
        /// </summary>
        public required bool AuthenticateCoPilotAgentEndpoint { get; set; } = true;
    }
}