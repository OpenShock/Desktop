using OpenShock.Desktop.ModuleBase.Navigation;

namespace OpenShock.Desktop.ModuleBase;

public abstract class DesktopModuleBase
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public virtual string? IconPath { get; } = null;

    public IModuleInstanceManager ModuleInstanceManager { get; set; } = null!;

    public abstract IReadOnlyCollection<NavigationItem> NavigationComponents { get; }

    public void SetContext(IModuleInstanceManager moduleInstanceManager)
    {
        ModuleInstanceManager = moduleInstanceManager;
    }
    
    public virtual Task Setup()
    {
        return Task.CompletedTask;
    }
    
    public virtual Task Start()
    {
        return Task.CompletedTask;
    }
}