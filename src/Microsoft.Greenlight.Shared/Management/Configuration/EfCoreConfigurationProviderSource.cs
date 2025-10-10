using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

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
                // Return a proxy provider that forwards to the real one once it's set
                return new EmptyConfigurationProvider(this);
            }

            return _providerInstance;
        }

        /// <summary>
        /// Proxy provider that forwards to the real provider once it's available.
        /// This ensures the configuration chain works even when the real provider is created later via DI.
        /// </summary>
        private class EmptyConfigurationProvider : ConfigurationProvider
        {
            private readonly EfCoreConfigurationProviderSource _source;

            public EmptyConfigurationProvider(EfCoreConfigurationProviderSource source)
            {
                _source = source;
            }

            public override bool TryGet(string key, out string? value)
            {
                if (_source._providerInstance != null)
                {
                    return _source._providerInstance.TryGet(key, out value);
                }
                value = null;
                return false;
            }

            public override void Set(string key, string? value)
            {
                if (_source._providerInstance != null)
                {
                    _source._providerInstance.Set(key, value);
                }
            }

            public override IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
            {
                if (_source._providerInstance != null)
                {
                    return _source._providerInstance.GetChildKeys(earlierKeys, parentPath);
                }
                return earlierKeys;
            }

            public override void Load()
            {
                // Delegate load to real provider if available
                if (_source._providerInstance != null)
                {
                    _source._providerInstance.Load();
                }
            }
        }
    }
}
