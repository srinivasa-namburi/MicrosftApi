using Microsoft.Extensions.Hosting;

namespace ProjectVico.V2.Shared;

public static class StartupDelay
{
    public static async Task DelayStartup(this IHostApplicationBuilder builder, bool isDurable)
    {
        if (!isDurable && builder.Environment.IsDevelopment())
        {
            Console.WriteLine($"Waiting for SetupManager to perform migrations and delete ServiceBus Topics/Queus for non-durable development");
            await Task.Delay(TimeSpan.FromSeconds(50));
        }
        else
        {
            Console.WriteLine($"Waiting for SetupManager to perform migrations...");
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}