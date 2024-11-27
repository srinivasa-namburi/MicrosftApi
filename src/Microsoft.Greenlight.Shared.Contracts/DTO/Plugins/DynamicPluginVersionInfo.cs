using System;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    public class DynamicPluginVersionInfo
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }

        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}