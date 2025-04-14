using Microsoft.Extensions.Configuration;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Helpers
{
    /// <summary>
    /// Provides helper methods for administrative tasks.
    /// </summary>
    public static class AdminHelper
    {
        private static IConfiguration? _configuration;

        /// <summary>
        /// Initializes the AdminHelper with the specified configuration.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        public static void Initialize(IConfiguration? configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Determines whether the application is running in a production environment.
        /// </summary>
        /// <returns><c>true</c> if the application is running in a production environment; otherwise, <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the AdminHelper is not initialized.</exception>
        public static bool IsRunningInProduction()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("AdminHelper is not initialized. Call Initialize method with IConfiguration.");
            }

            // Check for custom GREENLIGHT_PRODUCTION environment variable
            var greenlightProduction = _configuration["GREENLIGHT_PRODUCTION"];
            if (!string.IsNullOrEmpty(greenlightProduction) && !greenlightProduction.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Additional check for ASPNETCORE_ENVIRONMENT
            var aspNetCoreEnvironment = _configuration["ASPNETCORE_ENVIRONMENT"];
            if (!string.IsNullOrEmpty(aspNetCoreEnvironment) && !aspNetCoreEnvironment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Additional check for DOTNET_ENVIRONMENT
            var dotNetEnvironment = _configuration["DOTNET_ENVIRONMENT"];
            if (!string.IsNullOrEmpty(dotNetEnvironment) && !dotNetEnvironment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                return true;
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

        /// <summary>
        /// Determine the worker node type based on the currently running AppDomain
        /// </summary>
        /// <returns></returns>
        public static WorkerNodeType DetermineCurrentlyRunningWorkerNodeType()
        {
            // Get AppDomain friendly name
            var friendlyName = AppDomain.CurrentDomain.FriendlyName.ToLowerInvariant();

            // Check for Web node
            if (friendlyName.Contains("web."))
            {
                return WorkerNodeType.Web;
            }

            // Check for API node
            if (friendlyName.Contains("api."))
            {
                return WorkerNodeType.Api;
            }

            // Check for Worker node
            if (friendlyName.Contains("worker."))
            {
                return WorkerNodeType.Worker;
            }

            // Check for System node
            // ReSharper disable once StringLiteralTypo
            if (friendlyName.Contains("setupmanager."))
            {
                return WorkerNodeType.System;
            }

            // By default return Worker node
            return WorkerNodeType.Worker;

        }
    }
}