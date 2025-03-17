using Microsoft.Extensions.DependencyInjection;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Navigation;

namespace OpenShock.Desktop.ModuleBase;

public abstract class DesktopModuleBase
{
    public virtual string? IconPath { get; } = null;
    public IModuleInstanceManager ModuleInstanceManager { get; private set; } = null!;

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

    public IServiceProvider ModuleServiceProvider { get; protected set; } = new ServiceCollection().BuildServiceProvider();
}