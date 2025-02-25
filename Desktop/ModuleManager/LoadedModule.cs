using System.Reflection;
using OpenShock.Desktop.ModuleBase;
using Semver;

namespace OpenShock.Desktop.ModuleManager;

public sealed class LoadedModule
{
    public required ModuleAssemblyLoadContext LoadContext { get; init; }
    public required Assembly Assembly { get; init; }
    public required DesktopModuleBase Module { get; init; }
    public required SemVersion Version { get; init; }
}