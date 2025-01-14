using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared;

/// <summary>
/// Provides methods to delay the startup of the application.
/// </summary>
public static class StartupDelay
{
    /// <summary>
    /// Delays the startup of the application based on the environment and durability.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="isDurable">Indicates whether the application is durable.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task DelayStartup(this IHostApplicationBuilder builder, bool isDurable)
    {
        if (!isDurable && !AdminHelper.IsRunningInProduction())
        {
            Console.WriteLine($"Waiting for SetupManager to perform migrations and delete ServiceBus Topics/Queues for non-durable development");
            await Task.Delay(TimeSpan.FromSeconds(120));
        }
        else
        {
            Console.WriteLine($"Waiting for SetupManager to perform migrations...");
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}
