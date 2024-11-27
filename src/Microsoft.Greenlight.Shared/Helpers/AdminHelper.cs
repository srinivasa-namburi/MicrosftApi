using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.Shared.Helpers
{
    public static class AdminHelper
    {
        private static IConfiguration? _configuration;

        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public static bool IsRunningInProduction()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("AdminHelper is not initialized. Call Initialize method with IConfiguration.");
            }

            // Check for Azure Container Apps specific environment variable
            var azureContainerApp1 = _configuration["CONTAINER_APP_ENV"];
            if (!string.IsNullOrEmpty(azureContainerApp1))
            {
                return true;
            }

            // Check for Azure Container Apps specific environment variable
            var azureContainerApp2 = _configuration["WEBSITE_INSTANCE_ID"];
            if (!string.IsNullOrEmpty(azureContainerApp2))
            {
                return true;
            }

            // Additional check for ASPNETCORE_ENVIRONMENT
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            if (!string.IsNullOrEmpty(environment) && environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var separators = new[] { "__", ":", "::" };

            foreach (var separator in separators)
            {
                var webDocgenHttps = _configuration[$"services{separator}web-docgen{separator}https{separator}0"];
                if (webDocgenHttps != null && !webDocgenHttps.Contains("localhost"))
                {
                    return true;
                }

                var apiMainHttps = _configuration[$"services{separator}api-main{separator}https{separator}0"];
                if (apiMainHttps != null && !apiMainHttps.Contains("localhost"))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}