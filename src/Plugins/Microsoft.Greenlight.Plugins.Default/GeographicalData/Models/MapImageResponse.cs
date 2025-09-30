// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Greenlight.Plugins.Default.GeographicalData.Models;

/// <summary>
/// Response containing the URL path to a generated map image.
/// </summary>
public class MapImageResponse
{
    /// <summary>
    /// The relative URL path to access the map image.
    /// This path is ready to use in HTML img tags or markdown image syntax.
    /// Format: /api/file/download/external-asset/{guid}
    /// Do not modify this path - use it exactly as returned.
    /// </summary>
    public string ImageUrlPath { get; set; } = string.Empty;

    /// <summary>
    /// The latitude coordinate of the map center.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// The longitude coordinate of the map center.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// The zoom level used for the map.
    /// </summary>
    public string ZoomLevel { get; set; } = string.Empty;

    /// <summary>
    /// Width of the generated image in pixels.
    /// </summary>
    public int ImageWidth { get; set; }

    /// <summary>
    /// Height of the generated image in pixels.
    /// </summary>
    public int ImageHeight { get; set; }
}