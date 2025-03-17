using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Navigation;
using OpenShock.Desktop.Modules.ExampleModule;

[assembly:DesktopModule(typeof(ExampleDesktopModule), "openshock.desktop.modules.examplemodule", "Example Module")]
namespace OpenShock.Desktop.Modules.ExampleModule;

public class ExampleDesktopModule : DesktopModuleBase
{
    public override string IconPath => "OpenShock/Desktop/Modules/ExampleModule/Icon.svg";

    public ExampleDesktopModule()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ExampleService>();
        
        ModuleServiceProvider = services.BuildServiceProvider();
    }
    
    public override IReadOnlyCollection<NavigationItem> NavigationComponents { get; } =
    [
        new NavigationItem
        {
            Name = "Subcomponent 1",
            ComponentType = typeof(SubComponent1),
            Icon = IconOneOf.FromPath("OpenShock/Desktop/Modules/ExampleModule/Icon.svg")
        },
        new NavigationItem
        {
            Name = "Subcomponent 2",
            ComponentType = typeof(SubComponent2),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.Refresh)
        }
    ];

    public override async Task Start()
    {
        var config = await ModuleInstanceManager.GetModuleConfig<ExampleModuleConfig>();
        
        Console.WriteLine(config.Config.SomeConfigOption);
    }
}