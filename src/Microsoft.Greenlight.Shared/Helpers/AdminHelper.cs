namespace Microsoft.Greenlight.Shared.Helpers;

public static class AdminHelper
{
    public static bool IsRunningInProduction()
    {
        var separators = new[] { "__", ":", "::" };

        foreach (var separator in separators)
        {
            var webDocgenHttps = Environment.GetEnvironmentVariable($"services{separator}web-docgen{separator}https{separator}0");
            if (webDocgenHttps != null && !webDocgenHttps.Contains("localhost"))
            {
                return true;
            }
            
            var apiMainHttps = Environment.GetEnvironmentVariable($"services{separator}api-main{separator}https{separator}0");
            if (apiMainHttps != null && !apiMainHttps.Contains("localhost"))
            {
                return true;
            }
        }

        // Additional check for the presence of CONTAINER_APP_HOSTNAME, which indicates if the app is running in a container under Azure Container Apps
        var containerAppHostName = Environment.GetEnvironmentVariable("CONTAINER_APP_HOSTNAME");
        if (!string.IsNullOrEmpty(containerAppHostName))
        {
            return true;
        }

        return false;
    }
}