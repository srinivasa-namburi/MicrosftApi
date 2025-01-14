using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Shared.Core;

/// <summary>
/// A dynamic application builder for the project that wraps around the HostApplicationBuilder.
/// </summary>
public class GreenlightDynamicApplicationBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _innerBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="GreenlightDynamicApplicationBuilder"/> class.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public GreenlightDynamicApplicationBuilder(string[] args)
    {
        _innerBuilder = new HostApplicationBuilder(args);

        Properties = new Dictionary<object, object>();

        // Set the custom service provider factory explicitly
        this.ConfigureContainer<IServiceCollection>(
            new DynamicPluginLoadingServiceProviderFactory(new DefaultServiceProviderFactory())
        );
    }

    /// <summary>
    /// Gets the configuration manager.
    /// </summary>
    public IConfigurationManager Configuration => _innerBuilder.Configuration;

    /// <summary>
    /// Gets the host environment.
    /// </summary>
    public IHostEnvironment Environment => _innerBuilder.Environment;

    /// <summary>
    /// Gets the properties dictionary.
    /// </summary>
    public IDictionary<object, object> Properties { get; }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => _innerBuilder.Services;

    /// <summary>
    /// Gets the logging builder.
    /// </summary>
    public ILoggingBuilder Logging => _innerBuilder.Logging;

    /// <summary>
    /// Gets the metrics builder.
    /// </summary>
    public IMetricsBuilder Metrics => _innerBuilder.Metrics;

    /// <summary>
    /// Configures the container with the specified factory and optional configuration action.
    /// </summary>
    /// <typeparam name="TContainerBuilder">The type of the container builder.</typeparam>
    /// <param name="factory">The service provider factory.</param>
    /// <param name="configure">The optional configuration action.</param>
    public void ConfigureContainer<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory,
        Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
    {
        _innerBuilder.ConfigureContainer(factory, configure);
    }

    /// <summary>
    /// Builds the host.
    /// </summary>
    /// <returns>The built host.</returns>
    public IHost Build()
    {
        return _innerBuilder.Build();
    }
}
