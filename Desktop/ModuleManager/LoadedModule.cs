using System.Reflection;
using System.Runtime.Loader;
using ModuleBase;

namespace OpenShock.Desktop.ModuleManager;

public sealed class LoadedModule
{
    public required ModuleAssemblyLoadContext LoadContext { get; init; }
    public required Assembly Assembly { get; init; }
    public required IModule Module { get; init; }
}