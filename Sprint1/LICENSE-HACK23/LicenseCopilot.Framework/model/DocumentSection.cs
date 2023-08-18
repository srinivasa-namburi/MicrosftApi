using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseCopilot.Framework.Model;

public class DocumentSection
{
    public string Text { get; set; }

    public int StartPage { get; set; }

    public int StartIndexOnPage { get; set; }
}
