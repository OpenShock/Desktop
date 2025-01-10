using OneOf;
using Semver;

namespace OpenShock.Desktop.Config;

public class ModuleTask : OneOfBase<InstallModuleTask, RemoveModuleTask>
{
    public ModuleTask(OneOf<InstallModuleTask, RemoveModuleTask> input) : base(input)
    {
    }
    
    public static implicit operator ModuleTask(InstallModuleTask input) => new(input);
    public static implicit operator ModuleTask(RemoveModuleTask input) => new(input);
}

public struct InstallModuleTask
{
    public required SemVersion Version { get; set; }
}
    
public struct RemoveModuleTask
{
        
}