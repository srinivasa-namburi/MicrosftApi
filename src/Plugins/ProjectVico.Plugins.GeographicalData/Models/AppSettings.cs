// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Configuration;

namespace ProjectVico.Plugins.GeographicalData.Models;

public class AppSettings
{
    public const string DefaultConfigFile = "appsettings.json";
    public AIPluginSettings AIPlugin { get; set; }

    /// <summary>
    /// Load the kernel settings from appsettings.json if the file exists and if not attempt to use user secrets.
    /// </summary>
    public static AppSettings LoadSettings()
    {
        try
        {
            var appSettings = FromFile(DefaultConfigFile);

            return appSettings;
        }
        catch (InvalidDataException ide)
        {
            Console.Error.WriteLine(
                "Unable to load app settings.\n"
            );
            throw new InvalidOperationException(ide.Message);
        }
    }

    /// <summary>
    /// Load the kernel settings from the specified configuration file if it exists.
    /// </summary>
    private static AppSettings FromFile(string configFile = DefaultConfigFile)
    {
        if (!File.Exists(configFile))
        {
            throw new FileNotFoundException($"Configuration not found: {configFile}");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configFile, optional: true, reloadOnChange: true)
            .Build();

        return configuration.Get<AppSettings>()
               ?? throw new InvalidDataException($"Invalid app settings in '{configFile}'.");
    }
}
