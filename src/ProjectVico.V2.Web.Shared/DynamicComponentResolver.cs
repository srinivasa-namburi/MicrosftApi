using System.Reflection;

namespace ProjectVico.V2.Web.Shared;

public class DynamicComponentResolver
{
    public Type? GetDynamicComponent(string assemblyName, string namespaceName, string componentName)
    {
        var type = Type.GetType($"@namespaceName.{componentName}, assemblyName");
        if (type == null)
        {
            var assembly = Assembly.Load(assemblyName);
            type = assembly.GetType($"{namespaceName}.{componentName}");
        }

        return type;

    }
}