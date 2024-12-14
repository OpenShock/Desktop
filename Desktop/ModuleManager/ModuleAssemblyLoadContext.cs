using System.Reflection;

namespace OpenShock.Desktop.ModuleManager;

public class ModuleAssemblyLoadContext : System.Runtime.Loader.AssemblyLoadContext
{
    private readonly string _modulePath;
    private readonly string _moduleLibPath;

    public ModuleAssemblyLoadContext(string modulePath)
    {
        _modulePath = modulePath;
        _moduleLibPath = Path.Combine(modulePath, "libs");
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Construct the path to the dependency
        var dependencyFilePath = Path.Combine(_moduleLibPath, $"{assemblyName.Name}.dll");

        if (File.Exists(dependencyFilePath))
        {
            // Load the dependency
            return LoadFromAssemblyPath(dependencyFilePath);
        }

        return null; // Allow fallback to default context if necessary
    }
}