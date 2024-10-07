using System.ComponentModel;
using System.Globalization;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Shared.Interfaces;

namespace Microsoft.Greenlight.Plugins.Default.Utility;

public class ConversionPlugin : IPluginImplementation
{
    [KernelFunction("ConvertStringToNumber")]
    [Description("Converts a string to a number of type double")]
    public double ConvertStringToNumber(
        [Description("The string to convert to a number of type double. The string must contain a valid number that is possible to convert to a double.")]
        string input)
    {
        // Convert commas to dots in the input string
        input = input.Replace(",", ".");
        return double.Parse(input, NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    [KernelFunction("ConvertNumberToString")]
    [Description("Converts a number of type double to a string")]
    public string ConvertNumberToString(
        [Description("The number to convert to a string. The number must be a valid number of type double.")]
        double input)
    {
        return input.ToString(CultureInfo.InvariantCulture);
    }

    [KernelFunction("ConvertStringToInteger")]
    [Description("Converts a string to a number of type integer")]
    public int ConvertStringToInteger(
        [Description("The string to convert to a number of type integer. The string must contain a valid number that is possible to convert to an integer. It can't contain decimal separators.")]
        string input)
    {
        return Convert.ToInt32(input);
    }

    [KernelFunction("ConvertIntegerToString")]
    [Description("Converts a number of type integer to a string")]
    public string ConvertIntegerToString(
        [Description("The integer to convert to a string. Must be a valid integer - a number without decimal separators.")] 
        int input)
    {
        return input.ToString();
    }
}
