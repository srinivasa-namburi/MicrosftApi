using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Greenlight.Shared.Management.Configuration
{
    public class EfCoreConfigurationProviderSource : IConfigurationSource
    {
        private readonly IServiceCollection _services;
        private EfCoreConfigurationProvider? _providerInstance;

        public EfCoreConfigurationProviderSource(IServiceCollection services)
        {
            _services = services;
        }

        public void SetProviderInstance(EfCoreConfigurationProvider providerInstance)
        {
            _providerInstance = providerInstance;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            if (_providerInstance == null)
            {
                var serviceProvider = _services.BuildServiceProvider();
                _providerInstance = serviceProvider.GetRequiredService<EfCoreConfigurationProvider>();
            }
        
            return _providerInstance;
        }
    }
}