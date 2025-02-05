using MudBlazor;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Navigation;

namespace OpenShock.Desktop.Modules.ExampleModule;

public class ExampleDesktopModule : DesktopModuleBase
{
    public override string Id => "OpenShock.Desktop.Modules.ExampleModule";
    public override string Name => "Example Module";
    public override string IconPath => "OpenShock/Desktop/Modules/ExampleModule/Icon.svg";
    
    public override IReadOnlyCollection<NavigationItem> NavigationComponents =>
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
            Icon = IconOneOf.FromBase64(Icons.Material.Filled.Refresh)
        }
    ];

    public ExampleDesktopModule()
    {
        
    }
}