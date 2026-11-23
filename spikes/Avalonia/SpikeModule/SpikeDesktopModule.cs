using Microsoft.Extensions.DependencyInjection;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Navigation;
using OpenShock.Spike.AvaloniaModule;
using OpenShock.Spike.AvaloniaModule.Views;

[assembly: DesktopModule(typeof(SpikeDesktopModule), "openshock.spike.avaloniamodule", "Avalonia Spike Module")]

namespace OpenShock.Spike.AvaloniaModule;

public sealed class SpikeDesktopModule : DesktopModuleBase
{
    public SpikeDesktopModule()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SpikeService>();
        ModuleServiceProvider = services.BuildServiceProvider();
    }

    // Unchanged ModuleBase API: ComponentType is just a Type, here an Avalonia UserControl.
    public override IReadOnlyCollection<NavigationItem> NavigationComponents { get; } =
    [
        new NavigationItem { Name = "Spike Page", ComponentType = typeof(ModulePage) }
    ];
}

public sealed class SpikeService
{
    public string Greeting => "Hello from a module service, injected with [ModuleInject]";
}
