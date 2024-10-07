// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Greenlight.Plugins.Default.EarthQuake.Models;

public class EarthquakeDetail
{
    /// <summary>
    /// Timestamp of earthquake expressed as UTC date/time
    /// </summary>

    public DateTime TimeUtc { get; set; }

    /// <summary>
    /// Location of earthquake
    /// </summary>
    public string? Place { get; set; }

    /// <summary>
    /// Magnitude of earthquake
    /// <summary>
    public float? Magnitude { get; set; }

    /// <summary>
    /// Depth of earthquake
    /// </summary>
    public float? Depth { get; set; }

    /// <summary>
    /// Whether earthquake generated a tsunami
    /// </summary>
    public bool? Tsunami { get; set; }

    /// <summary>
    /// URL providing info about earthquake
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Latitude of the earthquake epicenter
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude of the earthquake epicenter
    /// </summary>
    public double Longitude { get; set; }
}
