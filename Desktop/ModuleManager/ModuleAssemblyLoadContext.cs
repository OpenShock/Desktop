using System.Reflection;
using System.Runtime.InteropServices;

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
            return LoadFromAssemblyPath(dependencyFilePath);
        }

        // Check for runtime-specific assembly, allows us for cross-platform modules
        var runtimeSpecificAssembly = Path.Combine(_moduleLibPath, RuntimeInformation.RuntimeIdentifier, $"{assemblyName.Name}.dll");
        if (File.Exists(runtimeSpecificAssembly))
        {
            return LoadFromAssemblyPath(runtimeSpecificAssembly);
        }

        return null; // Allow fallback to default context if necessary
    }
}