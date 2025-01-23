using ModuleBase;

namespace OpenShock.Desktop.Modules.ExampleModule;

public class ExampleModule : IModule
{
    public string Id => "OpenShock.Desktop.Modules.ExampleModule";
    public string Name => "Example Module";
    public Type RootComponentType => typeof(ExampleRootComponent);
    public string IconPath => "OpenShock/Desktop/Modules/ExampleModule/Icon.svg";
    

}