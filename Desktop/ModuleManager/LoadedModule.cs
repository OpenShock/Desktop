using System.Reflection;
using OpenShock.Desktop.ModuleBase;

namespace OpenShock.Desktop.ModuleManager;

public sealed class LoadedModule
{
    public required ModuleAssemblyLoadContext LoadContext { get; init; }
    public required Assembly Assembly { get; init; }
    public required DesktopModuleBase Module { get; init; }
}