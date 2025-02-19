using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Shared.Core;

public class GreenlightDynamicWebApplicationBuilder : IHostApplicationBuilder
{
    private readonly WebApplicationBuilder _innerBuilder;

    public GreenlightDynamicWebApplicationBuilder(string[] args)
    {
        _innerBuilder = WebApplication.CreateBuilder(args);

        Properties = new Dictionary<object, object>();

        _innerBuilder.Host.UseServiceProviderFactory(
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
        _innerBuilder.Host.UseServiceProviderFactory(factory);
        if (configure != null)
        {
            _innerBuilder.Host.ConfigureContainer(configure);
        }
    }

    public WebApplication Build()
    {
        return _innerBuilder.Build();
    }
}