using System.Reflection;
using OpenShock.Desktop.ModuleBase;
using OpenShock.SDK.CSharp.Models;
using Semver;
using Module = OpenShock.Desktop.ModuleManager.Repository.Module;

namespace OpenShock.Desktop.ModuleManager;

public sealed class LoadedModule
{
    public required ModuleAssemblyLoadContext LoadContext { get; init; }
    public required Assembly Assembly { get; init; }
    public required DesktopModuleAttribute ModuleAttribute { get; init; }
    public required DesktopModuleBase Module { get; init; }
    public required SemVersion Version { get; init; }
    
    public required Module? RepositoryModule { get; set; }
    public required SemVersion? AvailableVersion { get; set; }
    
    public required IReadOnlyList<PermissionType> RequiredPermissions { get; init; } = [];
    
    
    public string Id => ModuleAttribute.Id;
    public string Name => ModuleAttribute.Name;
}