using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared;

public static class StartupDelay
{
    public static async Task DelayStartup(this IHostApplicationBuilder builder, bool isDurable)
    {
        if (!isDurable && !AdminHelper.IsRunningInProduction())
        {
            Console.WriteLine($"Waiting for SetupManager to perform migrations and delete ServiceBus Topics/Queus for non-durable development");
            await Task.Delay(TimeSpan.FromSeconds(120));
        }
        else
        {
            Console.WriteLine($"Waiting for SetupManager to perform migrations...");
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}
