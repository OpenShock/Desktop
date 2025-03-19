# OpenShock Desktop Module Base

This module is the base for all OpenShock Desktop modules. It provides the basic structure and interfaces that all modules should implement.

## Installation

```bash
dotnet add package OpenShock.Desktop.ModuleBase
```

## Usage

You need to inherit the `DesktopModuleBase` class and add the `DesktopModuleAttribute` to your assembly.

A minimal module would look like this:

Note that the assembly attribute is needs to be outside a namespace to work.
```csharp
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Navigation;

[assembly:DesktopModule(typeof(ExampleDesktopModule), "openshock.desktop.modules.examplemodule", "Example Module")]
namespace OpenShock.Desktop.Modules.ExampleModule;

public class ExampleDesktopModule : DesktopModuleBase
{
    public override IReadOnlyCollection<NavigationItem> NavigationComponents { get; } = [];
}
```

 

Or see the example module on how to use it.