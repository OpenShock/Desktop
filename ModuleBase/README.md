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

## Module Packaging (`.module.zip`)

Starting with `OpenShock.Desktop.ModuleBase` 1.1.3, the NuGet package ships MSBuild
targets that automatically produce a ready-to-install `*.module.zip` bundle when you
publish your module. Adding the package reference is enough — no extra tooling, no
custom scripts.

### How it works

On `dotnet publish`, the `PackOpenShockModule` target runs after `Publish` and writes
`<AssemblyName>.module.zip` into the publish directory with this layout:

```
<AssemblyName>.dll                 # your module entry point (+ .pdb if present)
libs/
  *.dll                            # third-party deps the host does NOT already provide
  <rid>/…                          # native + RID-specific files copied from runtimes/
```

Assemblies the host already ships (MudBlazor, Serilog, the MAUI/Blazor stack,
OpenShock SDKs, `Microsoft.Extensions.*`, etc.) are **excluded automatically** so
your ZIP stays small and you don't risk loading a second copy at runtime. The
authoritative list lives in
[`OpenShock.Desktop.ModuleBase.targets`](build/OpenShock.Desktop.ModuleBase.targets)
as `OpenShockHostAssembly` items.

### Opting in / out

Packaging is on by default once you reference the NuGet package. To disable it for
a specific project, set:

```xml
<PropertyGroup>
  <OpenShockPackModule>false</OpenShockPackModule>
</PropertyGroup>
```

You can also customize:

| Property | Default | Purpose |
| --- | --- | --- |
| `OpenShockModuleId` | `$(AssemblyName)` | Stable module identifier used by the host. Set this to a reverse-DNS id like `openshock.desktop.modules.mymodule`. |
| `OpenShockModuleOutputDir` | `$(PublishDir)` | Where to drop the `.module.zip`. |

### Overriding a host-provided dependency

If your module genuinely needs a different version of something the host provides,
force it to be bundled:

```xml
<ItemGroup>
  <OpenShockBundleOverride Include="Newtonsoft.Json" />
</ItemGroup>
```

The override will be copied into `libs/` alongside your other dependencies. Use this
sparingly — the host may still reject your module if the mismatch affects a type it
hands you across the API boundary.

### Shipping extra files (native sidecars, assets)

If your publish output contains directories you need at runtime (e.g. `x64/` with
native DLLs), list them with `OpenShockExtraDir`:

```xml
<ItemGroup>
  <OpenShockExtraDir Include="x64" />
</ItemGroup>
```

Each listed directory is copied under `libs/<name>/…` in the final ZIP.

### Version-mismatch diagnostics (`OSMOD001`)

After publish, the target scans your `deps.json` and compares dependency versions
against the host's declared versions. A **major-version mismatch** emits MSBuild
warning `OSMOD001` at build time, e.g.:

```
warning OSMOD001: Module references MudBlazor 8.0.0 but host provides 9.3.0.
Major version mismatch may cause runtime errors.
```

Treat these as real — major bumps across the host/module boundary almost always
break at load time. Either align to the host version or force-bundle via
`OpenShockBundleOverride` (and accept the duplication).

### `ProjectReference` consumers

If you reference `ModuleBase` as a `ProjectReference` instead of a `PackageReference`
(as the in-repo `ExampleModule` does), MSBuild does not auto-import the NuGet `build/`
assets. Add the imports manually at the top and bottom of the csproj:

```xml
<Import Project="../ModuleBase/build/OpenShock.Desktop.ModuleBase.props" />
<!-- ... PropertyGroup / ItemGroup ... -->
<Import Project="../ModuleBase/build/OpenShock.Desktop.ModuleBase.targets" />
```

NuGet consumers get both imports for free.