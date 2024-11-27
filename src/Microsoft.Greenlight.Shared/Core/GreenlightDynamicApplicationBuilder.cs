using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Shared.Core;

public class GreenlightDynamicApplicationBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _innerBuilder;

    public GreenlightDynamicApplicationBuilder(string[] args)
    {
        _innerBuilder = new HostApplicationBuilder(args);
        
        Properties = new Dictionary<object, object>();

        // Set the custom service provider factory explicitly
        this.ConfigureContainer<IServiceCollection>(
            new DynamicPluginLoadingServiceProviderFactory(new DefaultServiceProviderFactory())
        );
    }

    public IConfigurationManager Configuration => _innerBuilder.Configuration;

    public IHostEnvironment Environment => _innerBuilder.Environment;

    public IDictionary<object, object> Properties { get; }

    public IServiceCollection Services => _innerBuilder.Services;

    public ILoggingBuilder Logging => _innerBuilder.Logging;

    public IMetricsBuilder Metrics => _innerBuilder.Metrics;

    public void ConfigureContainer<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory,
        Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
    {
        _innerBuilder.ConfigureContainer(factory, configure);
    }

    public IHost Build()
    {
        return _innerBuilder.Build();
    }
}