using System.Reflection;

namespace Microsoft.Greenlight.Web.Shared;

public class DynamicComponentResolver
{
    public Type? GetDynamicComponent(string assemblyName, string namespaceName, string componentName)
    {


        // First, try to get the type from the provided assembly and namespace
        var type = TryGetType(assemblyName, namespaceName, componentName);

        // If the type is not found, fallback to the default assembly and namespace
        if (type == null)
        {
            const string defaultAssemblyName = "Microsoft.Greenlight.UI.Default";
            namespaceName = defaultAssemblyName;
            type = TryGetType(defaultAssemblyName, namespaceName, componentName);
        }

        return type;
    }

    private Type? TryGetType(string asmName, string nsName, string compName)
    {
        var type = Type.GetType($"{nsName}.{compName}, {asmName}");
        if (type == null)
        {
            try
            {
                var assembly = Assembly.Load(asmName);
                type = assembly.GetType($"{nsName}.{compName}");
            }
            catch(FileNotFoundException ex)
            {
                return null;
            }
        }
        return type;
    }
}
