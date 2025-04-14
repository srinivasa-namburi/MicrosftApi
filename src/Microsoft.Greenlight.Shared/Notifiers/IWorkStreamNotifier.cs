using Orleans.Streams;

namespace Microsoft.Greenlight.Shared.Notifiers
{
    /// <summary>
    /// Interface for workstream notifiers that handle Orleans stream subscriptions for specific domains
    /// </summary>
    public interface IWorkStreamNotifier
    {
        /// <summary>
        /// Sets up all required stream subscriptions for this domain
        /// </summary>
        /// <param name="clusterClient">The Orleans cluster client</param>
        /// <param name="streamProvider">Stream provider to use</param>
        /// <returns>List of objects representing subscription handles that will be managed</returns>
        Task<List<object>> SubscribeToStreamsAsync(
            IClusterClient clusterClient,
            IStreamProvider streamProvider);
            
        /// <summary>
        /// Gets the name of this work stream notifier
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Unsubscribes from all streams handled by this notifier
        /// </summary>
        /// <param name="subscriptionHandles">The handles to unsubscribe</param>
        Task UnsubscribeFromStreamsAsync(IEnumerable<object> subscriptionHandles);
    }
}