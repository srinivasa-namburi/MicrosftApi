using System.ComponentModel;
using System.Globalization;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.SemanticKernel;

namespace Microsoft.Greenlight.Plugins.Default.Utility;
public class DatePlugin : IPluginImplementation
{
    [KernelFunction(("GetCurrentDate"))]
    [Description("Returns the current date as a string in the format yyyy-MM-dd")]
    public string GetCurrentDate()
    {
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    [KernelFunction(("GetCurrentYearAsString"))]
    [Description("Returns the current year as a string")]
    public string GetCurrentYearAsString()
    {
        return DateTime.Now.Year.ToString();
    }

    [KernelFunction(("GetCurrentMonthNumberAsString"))]
    [Description("Returns the current month number as a string")]
    public string GetCurrentMonthNumberAsString()
    {
        return DateTime.Now.Month.ToString();
    }

    [KernelFunction(("GetCurrentMonthName"))]
    [Description("Returns the name of the current month")]
    public string GetCurrentMonthName()
    {
        return DateTime.Now.ToString("MMMM", CultureInfo.InvariantCulture);
    }

    [KernelFunction(("GetCurrentTimeIn24HourFormat"))]
    [Description("Returns the current time in the 24-hour format HH:mm:ss")]
    public string GetCurrentTimeIn24HourFormat()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }

    [KernelFunction(("GetCurrentTimeIn12HourFormat"))]
    [Description("Returns the current time in the 12-hour format hh:mm:ss tt")]
    public string GetCurrentTimeIn12HourFormat()
    {
        return DateTime.Now.ToString("hh:mm:ss tt");
    }
}
