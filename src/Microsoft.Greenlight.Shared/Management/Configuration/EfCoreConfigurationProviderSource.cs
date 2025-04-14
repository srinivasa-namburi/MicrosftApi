using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.Shared.Management.Configuration
{
    /// <summary>
    /// Custom Entity Framework Core configuration source.
    /// </summary>
    public class EfCoreConfigurationProviderSource : IConfigurationSource
    {
        private EfCoreConfigurationProvider? _providerInstance;
    
        public void SetProviderInstance(EfCoreConfigurationProvider providerInstance)
        {
            _providerInstance = providerInstance;
        }
    
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            if (_providerInstance == null)
            {
                // Return a temporary no-op provider
                return new EmptyConfigurationProvider();
            }
        
            return _providerInstance;
        }
    
        private class EmptyConfigurationProvider : ConfigurationProvider
        {
            // This is a temporary no-op provider until the real one is set
        }
    }
}