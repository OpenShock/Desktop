using System.Reflection;
using OpenShock.Desktop.ModuleBase;
using Semver;
using Module = OpenShock.Desktop.ModuleManager.Repository.Module;

namespace OpenShock.Desktop.ModuleManager;

public sealed class LoadedModule
{
    public required ModuleAssemblyLoadContext LoadContext { get; init; }
    public required Assembly Assembly { get; init; }
    public required DesktopModuleBase Module { get; init; }
    public required SemVersion Version { get; init; }
    
    public required Module? RepositoryModule { get; set; }
    public required SemVersion? AvailableVersion { get; set; }
}