using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using System.Text.Json;

namespace Microsoft.Greenlight.Shared.Management.Configuration;

/// <summary>
/// Configuration provider that uses Entity Framework Core.
/// </summary>
public class EfCoreConfigurationProvider : ConfigurationProvider
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<EfCoreConfigurationProvider> _logger;
    private readonly IOptionsMonitor<ServiceConfigurationOptions> _optionsMonitor;
    private readonly IConfigurationRoot _configurationRoot;
    private bool _isLoading;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreConfigurationProvider"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="optionsMonitor">The options monitor.</param>
    /// <param name="configurationRoot">The configuration root.</param>
    public EfCoreConfigurationProvider(
        DocGenerationDbContext dbContext, 
        ILogger<EfCoreConfigurationProvider> logger, 
        IOptionsMonitor<ServiceConfigurationOptions> optionsMonitor,
        IConfigurationRoot configurationRoot)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _configurationRoot = configurationRoot ?? throw new ArgumentNullException(nameof(configurationRoot));

        // Subscribe to changes in the options
        _optionsMonitor.OnChange(options =>
        {
            // Handle the options change if needed
            _logger.LogInformation("OptionsMonitor change event occured");
        });
    }

    /// <summary>
    /// Loads configuration values from the database.
    /// </summary>
    public override void Load()
    {
        if (_isLoading)
        {
            return;
        }

        try
        {
            _isLoading = true;

            var configuration = _dbContext.Configurations.FirstOrDefault(c => c.Id == 1);

            if (configuration != null)
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    configuration.ConfigurationValues,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Dictionary<string, string>();

                Data = values;
                _logger.LogInformation("Loaded {Count} configuration values from database", values.Count);

                // Force options update using the configuration system
                UpdateOptions(values);
            }
            else
            {
                _logger.LogWarning("No configuration found in the database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from database");
        }
        finally
        {
            _isLoading = false;
        }
    }
   
    /// <summary>
    /// Updates the options with the specified values using the configuration system.
    /// </summary>
    /// <param name="values">Dictionary of configuration key-value pairs.</param>
    public void UpdateOptions(Dictionary<string, string> values)
    {
        try
        {
            bool hasChanges = false;
            foreach (var kvp in values)
            {
                if (_configurationRoot[kvp.Key] != kvp.Value)
                {
                    _configurationRoot[kvp.Key] = kvp.Value;
                    Data[kvp.Key] = kvp.Value;
                    hasChanges = true;
                }
            }
        
            if (hasChanges)
            {
                OnReload();
                _logger.LogInformation("Reloaded configuration with {Count} updated values", values.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating options through configuration");
        }
    }
}

