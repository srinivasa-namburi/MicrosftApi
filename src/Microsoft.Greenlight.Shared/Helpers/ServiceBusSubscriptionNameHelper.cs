using Microsoft.Greenlight.Shared.Contracts.Messages;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Greenlight.Shared.Helpers
{
    /// <summary>
    /// Provides helper methods for generating service bus subscription names.
    /// </summary>
    public static class ServiceBusSubscriptionNameHelper
    {
        /// <summary>
        /// Gets the endpoint name for the <see cref="RestartWorker"/>.
        /// </summary>
        /// <returns>The endpoint name as a string.</returns>
        /// <param name="subscriptionPrefix">Prefix to uniquely identify the type of subscription. Don't use more than 3 letters.</param>
        public static string GetWorkerEndpointName(string subscriptionPrefix)
        {
            var domainName = AppDomain.CurrentDomain.FriendlyName;

            // Get only the last part from the full domainName
            var domainNameParts = domainName.Split('.');
            var domainNameShort = domainNameParts[^1];

            //Compute an MD5 hash based on the machine name
            var machineName = Environment.MachineName;
            var machineNameHashBytes = MD5.HashData(Encoding.UTF8.GetBytes(machineName));
            var machineNameHash = BitConverter.ToString(machineNameHashBytes).Replace("-", "").ToLower();

            machineNameHash = machineNameHash.Substring(0, 14);

            // Computed subscription name

            var processId = Environment.ProcessId;
            var subscriptionName = $"{subscriptionPrefix}-{domainNameShort}-{machineNameHash}-{processId}";

            return subscriptionName;
        }

        /// <summary>
        /// Gets the endpoint name for the <see cref="RestartWorker"/> message.
        /// </summary>
        /// <returns>A fully computed subscription name for this instance of the <see cref="RestartWorker"/> command</returns>
        public static string GetRestartWorkerEndpointName()
        {
            return GetWorkerEndpointName("rw");
        }
    }
}