using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

public class PluginSourceReferenceItem : SourceReferenceItem
{
    public string? PluginIdentifier { get; set; }
    public override string? SourceOutput { get; set; }
    public string SourceInputJson { get; set; }
    [NotMapped]
    public bool SourceOutputIsValidJson => IsValidJson(SourceOutput);

    public override void SetBasicParameters()
    {
        SourceReferenceType = SourceReferenceType.Plugin;
        Description = "JSON or string output from plugin";
    }

    public void SetSourceOutput(string output)
    {
        // Store the JSON output in the SourceOutput property
        SourceOutput = output;
    }

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

    private T? GetSourceOutput<T>()
    {
        // Deserialize the JSON output to the specified type
        return SourceOutput != null ? JsonSerializer.Deserialize<T>(SourceOutput) : default;
    }

}