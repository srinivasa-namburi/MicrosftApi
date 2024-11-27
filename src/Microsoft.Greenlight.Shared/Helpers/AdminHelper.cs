namespace Microsoft.Greenlight.Shared.Helpers;

public static class AdminHelper
{
    public static bool IsRunningInProduction()
    {
        var separators = new[] { ":", "__" };

        foreach (var separator in separators)
        {
            var apiMainHttps = Environment.GetEnvironmentVariable($"services{separator}api-main{separator}https{separator}0");
            if (apiMainHttps != null && apiMainHttps.Contains("azurecontainerapps"))
            {
                return true;
            }

            var webDocgenHttps = Environment.GetEnvironmentVariable($"services{separator}web-docgen{separator}https{separator}0");
            if (webDocgenHttps != null && webDocgenHttps.Contains("azurecontainerapps"))
            {
                return true;
            }
        }

        
    
        return false;
    }
}