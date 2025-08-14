using Microsoft.Extensions.DependencyInjection;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Navigation;

namespace OpenShock.Desktop.ModuleBase;

/// <summary>
/// The main base class for all desktop modules.
/// </summary>
public abstract class DesktopModuleBase
{
    /// <summary>
    /// Icon path for the module, used in navigation and other UI components.
    /// </summary>
    public virtual string? IconPath { get; } = null;
    
    /// <summary>
    /// Main interface for modules to interact with OpenShock Desktop.
    /// </summary>
    public IModuleInstanceManager ModuleInstanceManager { get; private set; } = null!;
    
    /// <summary>
    /// A collection of navigation components that this module provides.
    /// These components will be displayed in the main navigation menu of the application.
    /// </summary>
    public abstract IReadOnlyCollection<NavigationItem> NavigationComponents { get; }

    /// <summary>
    /// Alternative to <see cref="NavigationComponents"/> for modules that have a single main component.
    /// </summary>
    public virtual Type? RootComponent { get; } = null;

    /// <summary>
    /// This method is called by OpenShock Desktop to set the context for the module. Do not call this method by yourself.
    /// </summary>
    /// <param name="moduleInstanceManager"></param>
    public void SetContext(IModuleInstanceManager moduleInstanceManager)
    {
        ModuleInstanceManager = moduleInstanceManager;
    }
    
    /// <summary>
    /// During startup, this method is called to allow the module to perform any necessary setup.
    /// </summary>
    /// <returns></returns>
    public virtual Task Setup()
    {
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Called after <see cref="Setup"/> to allow the module to start any necessary services or perform additional initialization.
    /// </summary>
    /// <returns></returns>
    public virtual Task Start()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Module service provider.
    /// You can use the <see cref="ModuleInjectAttribute"/> to resolve services that are registered for this module within blazor components.
    ///
    /// <example>
    /// <code>
    /// [ModuleInject] private IModuleConfig<SomeModuleConfig> ModuleConfig { get; set; } = null!;
    /// </code>
    /// </example>
    /// 
    /// </summary>
    public IServiceProvider ModuleServiceProvider { get; protected set; } = new ServiceCollection().BuildServiceProvider();
}