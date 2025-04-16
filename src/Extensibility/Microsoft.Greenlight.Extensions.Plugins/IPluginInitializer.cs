namespace Microsoft.Greenlight.Extensions.Plugins
{
    /// <summary>
    /// Responsible for initializing a plugin after the final ServiceProvider is built.
    /// This is where you can resolve dependencies needed for the plugin. 
    /// </summary>
    public interface IPluginInitializer
    {
        /// <summary>
        /// Called after the final ServiceProvider is built so the plugin
        /// can resolve its dependencies and create its instance.
        /// </summary>
        Task InitializeAsync(IServiceProvider serviceProvider);
    }
}