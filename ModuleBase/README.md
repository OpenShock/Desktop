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

## Pages

A module shows its pages through `NavigationComponents` (one sidebar entry per page) or `RootComponent` (a single
page). A page type can be either of these, and you can mix them per page:

- **An Avalonia control** (native, recommended for new modules). Any non-abstract `Avalonia.Controls.Control`, for
  example a `UserControl` with XAML. The app draws it itself.
- **A Blazor component** (legacy). Any `IComponent`, as before. The app shows it in a webview, so existing modules
  keep working unchanged.

### Native (Avalonia) pages

Reference Avalonia so it is compiled against but never shipped. The app provides it, and your module uses the app's
copy:

```xml
<PackageReference Include="Avalonia" Version="12.2.0" ExcludeAssets="runtime" PrivateAssets="all" />
```

Then point a navigation item at your control:

```csharp
new NavigationItem
{
    Name = "Settings",
    ComponentType = typeof(SettingsPage), // a UserControl
    Icon = IconOneOf.FromPath("avares://My.Module/Assets/Icon.svg")
}
```

How the app builds and hosts the page:

- **Services.** Constructor parameters are resolved from your `ModuleServiceProvider` first, then from the app's
  services (`ILogger<T>` comes from there). Properties marked `[ModuleInject]` are then filled from your
  `ModuleServiceProvider`, the same way as for Blazor pages. They are set *after* the constructor runs, so read them
  in `OnLoaded` or later, not in the constructor. If there are several constructors, mark the one to use with
  `[ActivatorUtilitiesConstructor]`.
- **Lifetime.** A new instance is created each time the page is opened. When the user leaves it, it is removed, and
  disposed if it implements `IDisposable`.
- **Layout.** The page gets the whole page area with a 24px margin. It is not wrapped in a scroll viewer, so add a
  `ScrollViewer` yourself if your content can be taller than the window.
- **Errors.** If the page throws while being created, or an exception from your code reaches the UI thread unhandled,
  the app closes that page and shows the error with an "Open again" button. The rest of the app keeps running.
- **Look.** Your XAML can use the app's resources and style classes, so the page matches the rest of the app:
  - Brushes, used with `DynamicResource` so they follow the theme: `BackgroundBrush`, `SurfaceBrush`, `PrimaryBrush`,
    `TextPrimaryBrush`, `TextSecondaryBrush`, `DividerBrush`, `HoverBrush`, `SuccessBrush`, `InfoBrush`,
    `WarningBrush`, `ErrorBrush`.
  - Text classes: `h5`, `h6`, `subtitle2`, `body2`, `caption`, `secondary`.
  - Button classes: `filled`, `outlined`, `text`, `small`, and `primary` / `success` / `warning` / `info` as colours.
  - Switch: `CheckBox` with class `switch`.
- **Icons.** Navigation icons can be `avares://` paths to SVGs or images in your module (as `AvaloniaResource`), next
  to the embedded resource paths and SVG strings that already work.

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

Assemblies the host already ships (MudBlazor, Serilog, the Avalonia/Blazor stack,
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