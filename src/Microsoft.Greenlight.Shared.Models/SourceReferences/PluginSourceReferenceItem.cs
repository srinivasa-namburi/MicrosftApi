using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

/// <summary>
/// Represents a plugin source reference item.
/// </summary>
public class PluginSourceReferenceItem : SourceReferenceItem
{
    /// <summary>
    /// Identifier of the plugin.
    /// </summary>
    public string? PluginIdentifier { get; set; }

    /// <summary>
    /// Source output from the plugin.
    /// </summary>
    public override string? SourceOutput { get; set; }

    /// <summary>
    /// Gets or sets the source input JSON.
    /// </summary>
    public string? SourceInputJson { get; set; }

    /// <summary>
    /// Gets a value indicating whether the source output is valid JSON.
    /// </summary>
    [NotMapped]
    public bool SourceOutputIsValidJson => IsValidJson(SourceOutput);

    /// <summary>
    /// Sets the basic parameters for the plugin source reference item.
    /// </summary>
    public override void SetBasicParameters()
    {
        SourceReferenceType = SourceReferenceType.Plugin;
        Description = "JSON or string output from plugin";
    }

    /// <summary>
    /// Sets the source output.
    /// </summary>
    /// <param name="output">The output to set.</param>
    public void SetSourceOutput(string output)
    {
        SourceOutput = output;
    }

    /// <summary>
    /// Determines whether the specified string is valid JSON.
    /// </summary>
    /// <param name="stringToTest">The string to test.</param>
    /// <returns><c>true</c> if the specified string is valid JSON; otherwise, <c>false</c>.</returns>
    private bool IsValidJson(string? stringToTest)
    {
        if (stringToTest == null)
        {
            return false;
        }
        try
        {
            GetSourceOutput<object>();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the source output deserialized to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The deserialized source output.</returns>
    private T? GetSourceOutput<T>()
    {
        return SourceOutput != null ? JsonSerializer.Deserialize<T>(SourceOutput) : default;
    }
}
