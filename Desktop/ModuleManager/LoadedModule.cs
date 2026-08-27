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

    /// <summary>
    /// The module's navigation and page metadata, read once at load time. Always use this instead of
    /// going to <see cref="Module"/> from the UI, see <see cref="ModuleUiSurface"/>.
    /// </summary>
    public ModuleUiSurface Ui { get; internal set; } = ModuleUiSurface.Empty;


    public string Id => ModuleAttribute.Id;
    public string Name => ModuleAttribute.Name;
}
