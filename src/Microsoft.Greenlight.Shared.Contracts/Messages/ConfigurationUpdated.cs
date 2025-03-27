// Microsoft.Greenlight.Shared.Contracts/Messages/ConfigurationUpdated.cs
using System;

namespace Microsoft.Greenlight.Shared.Contracts.Messages
{
    /// <summary>
    /// Message published when configuration is updated.
    /// </summary>
    public record ConfigurationUpdated(Guid CorrelationId);
}
