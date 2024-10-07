namespace Microsoft.Greenlight.Extensions.Plugins
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GreenlightPluginAttribute : Attribute
    {
        public string Name { get; }
        public string RegistrationKey { get; set; }
        public string? Version { get; }
        public string? CopyrightHolder { get; }

        public GreenlightPluginAttribute(string name, string registrationKey, string? version = null,
            string? copyrightHolder = null)
        {
            Name = name;
            RegistrationKey = registrationKey;
            Version = version;
            CopyrightHolder = copyrightHolder;
        }
    }
}
