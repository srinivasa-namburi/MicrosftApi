using Humanizer;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared;

/// <summary>
/// Provides methods to delay the startup of the application.
/// </summary>
public static class StartupDelay
{
    /// <summary>
    /// Delays startup to wait for Orleans Silo to become available
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="isDurable">Indicates whether the application is durable.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task DelayStartup(this IHostApplicationBuilder builder, bool isDurable)
    {
        // Get the executing assembly
        var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();

        // Only perform the following actions for assemblies that are not
        // Microsoft.Greenlight.Silo
        
        if (!AdminHelper.IsRunningInProduction())
        {
            if (executingAssembly.GetName().Name != "Microsoft.Greenlight.Silo")
            {
                var timespan = 15.Seconds();
                // Delay startup for 30 seconds
                Console.WriteLine($"Waiting {timespan.Seconds} for Orleans Silo to become available");
                await Task.Delay(timespan);
            }
            
        }
    }
}
