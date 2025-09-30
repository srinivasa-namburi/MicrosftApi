using Microsoft.Extensions.Configuration;
using Microsoft.Greenlight.Shared.Configuration;
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
                // Check HTTPS URLs (development environment)
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

                // Check HTTP URLs (production environment - containers use HTTP internally)
                var webDocgenHttp = _configuration[$"services{separator}web-docgen{separator}http{separator}0"];
                if (webDocgenHttp != null && !webDocgenHttp.Contains("localhost"))
                {
                    return true;
                }

                var apiMainHttp = _configuration[$"services{separator}api-main{separator}http{separator}0"];
                if (apiMainHttp != null && !apiMainHttp.Contains("localhost"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the base URL for the API service, automatically selecting HTTP or HTTPS based on environment.
        /// </summary>
        /// <returns>The base URL for the API service.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the AdminHelper is not initialized.</exception>
        public static string GetApiServiceUrl()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("AdminHelper is not initialized. Call Initialize method with IConfiguration.");
            }

            if (IsRunningInProduction())
            {
                var url = _configuration["services:api-main:http:0"]
                    ?? _configuration["services__api_main__http__0"];

                // Fallback for Kubernetes where service discovery might not be configured yet
                return !string.IsNullOrEmpty(url) ? url : "http://api-main:8080";
            }
            else
            {
                return _configuration["services:api-main:https:0"]
                    ?? _configuration["services__api_main__https__0"]
                    ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the base URL for the Web DocGen service, automatically selecting HTTP or HTTPS based on environment.
        /// </summary>
        /// <returns>The base URL for the Web DocGen service.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the AdminHelper is not initialized.</exception>
        public static string GetWebDocGenServiceUrl()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("AdminHelper is not initialized. Call Initialize method with IConfiguration.");
            }

            if (IsRunningInProduction())
            {
                var url = _configuration["services:web-docgen:http:0"]
                    ?? _configuration["services__web_docgen__http__0"];

                // Fallback for Kubernetes where service discovery might not be configured yet
                return !string.IsNullOrEmpty(url) ? url : "http://web-docgen:8081";
            }
            else
            {
                return _configuration["services:web-docgen:https:0"]
                    ?? _configuration["services__web_docgen__https__0"]
                    ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the base URL for the Silo service, automatically selecting HTTP or HTTPS based on environment.
        /// </summary>
        /// <returns>The base URL for the Silo service.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the AdminHelper is not initialized.</exception>
        public static string GetSiloServiceUrl()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("AdminHelper is not initialized. Call Initialize method with IConfiguration.");
            }

            if (IsRunningInProduction())
            {
                var url = _configuration["services:silo:http:0"]
                    ?? _configuration["services__silo__http__0"];

                // Fallback for Kubernetes where service discovery might not be configured yet
                return !string.IsNullOrEmpty(url) ? url : "http://silo:8080";
            }
            else
            {
                return _configuration["services:silo:https:0"]
                    ?? _configuration["services__silo__https__0"]
                    ?? string.Empty;
            }
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

        /// <summary>
        /// Validates that the developer setup has been executed in non-production environments.
        /// If the setup has not been completed, displays an error message and exits the application.
        /// </summary>
        /// <param name="applicationName">The name of the application for display purposes.</param>
        /// <exception cref="InvalidOperationException">Thrown if the AdminHelper is not initialized.</exception>
        public static void ValidateDeveloperSetup(string applicationName = "Application")
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("AdminHelper is not initialized. Call Initialize method with IConfiguration.");
            }

            // Only check in non-production environments
            if (IsRunningInProduction())
            {
                return;
            }

            var serviceConfig = _configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>();
            var developerSetupExecuted = serviceConfig?.GreenlightServices?.Global?.DeveloperSetupExecuted ?? false;

            if (!developerSetupExecuted)
            {

                // If we're running in ADO Pipelines, skip the developer setup check
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")))
                {
                    return;
                }

                // If we're running in GitHub Actions, skip the developer setup check
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
                {
                    return;
                }

                // Detect if we're in a container environment where Unicode might not display properly
                bool isContainer = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")) ||
                                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("container")) ||
                                   File.Exists("/.dockerenv");

                Console.WriteLine();
                if (isContainer)
                {
                    Console.WriteLine("*** DEVELOPER SETUP REQUIRED ***");
                    Console.WriteLine("=================================================================");
                    Console.WriteLine();
                    Console.WriteLine($"   {applicationName} cannot start because the development");
                    Console.WriteLine("   environment has not been properly configured.");
                    Console.WriteLine();
                    Console.WriteLine("TO FIX THIS ISSUE:");
                    Console.WriteLine();
                    Console.WriteLine("   1. Run the appropriate setup script for your platform:");
                    Console.WriteLine();
                    Console.WriteLine("      Windows:");
                    Console.WriteLine("          scripts\\setup-dev-environment.ps1");
                    Console.WriteLine();
                    Console.WriteLine("      Linux/macOS:");
                    Console.WriteLine("          scripts/setup-dev-environment.sh");
                    Console.WriteLine();
                    Console.WriteLine("   2. Restart the application after the setup completes");
                    Console.WriteLine();
                    Console.WriteLine("WHAT THE SETUP SCRIPT DOES:");
                    Console.WriteLine("   * Installs required development tools (.NET SDK, Docker, VS Code)");
                    Console.WriteLine("   * Configures MCP (Model Context Protocol) for your platform");
                    Console.WriteLine("   * Sets up development configuration files");
                    Console.WriteLine("   * Creates the proper appsettings.Development.json file");
                }
                else
                {
                    Console.WriteLine("🚫 DEVELOPER SETUP REQUIRED");
                    Console.WriteLine("═══════════════════════════════════════════════════════════════");
                    Console.WriteLine();
                    Console.WriteLine($"   {applicationName} cannot start because the development");
                    Console.WriteLine("   environment has not been properly configured.");
                    Console.WriteLine();
                    Console.WriteLine("📋 TO FIX THIS ISSUE:");
                    Console.WriteLine();
                    Console.WriteLine("   1. Run the appropriate setup script for your platform:");
                    Console.WriteLine();
                    Console.WriteLine("      🖥️  Windows:");
                    Console.WriteLine("          scripts\\setup-dev-environment.ps1");
                    Console.WriteLine();
                    Console.WriteLine("      🐧 Linux/macOS:");
                    Console.WriteLine("          scripts/setup-dev-environment.sh");
                    Console.WriteLine();
                    Console.WriteLine("   2. Restart the application after the setup completes");
                    Console.WriteLine();
                    Console.WriteLine("💡 WHAT THE SETUP SCRIPT DOES:");
                    Console.WriteLine("   • Installs required development tools (.NET SDK, Docker, VS Code)");
                    Console.WriteLine("   • Configures MCP (Model Context Protocol) for your platform");
                    Console.WriteLine("   • Sets up development configuration files");
                    Console.WriteLine("   • Creates the proper appsettings.Development.json file");
                }
                Console.WriteLine();
                Console.WriteLine("=================================================================");
                Console.WriteLine();

                Environment.Exit(1);
            }
        }
    }
}