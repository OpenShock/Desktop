# Avalonia migration spikes

Throwaway code that answers the open questions in the Avalonia migration plan before any real
work starts. Not part of the product build (`Desktop.slnx` does not include it).

Everything here targets Avalonia 12.2.0 and Avalonia.Controls.WebView 12.1.2 on .NET 10.

## Spike 2: Avalonia views from modules (`SpikeModule`, `SpikeModule.Helpers`, `SpikeHost`)

Can a module ship an Avalonia page that is loaded through the real `ModuleAssemblyLoadContext`?

```sh
dotnet publish SpikeModule/SpikeModule.csproj -c Release -o out/module-publish
dotnet run --project SpikeHost -- out/module-publish/OpenShock.Spike.AvaloniaModule.module.zip out/work
# --window shows it in a real window instead of the headless checks
```

`SpikeModule` is packed by the real `PackOpenShockModule` target. `SpikeHost` extracts the zip the way
`ModuleManager` does, loads it with `Desktop/ModuleManager/ModuleAssemblyLoadContext.cs` (linked, not
copied), builds the page like `OpenShockModuleComponentActivator` (construct, then fill `[ModuleInject]`),
renders it headless with Skia and asserts on the result.

**Result: everything works with no change to the loader or to the ModuleBase API.**

- `NavigationItem.ComponentType` carries an Avalonia `UserControl` as-is.
- Compiled XAML, compiled bindings, `avares://` images, `StyleInclude`, merged `ResourceInclude` +
  `DynamicResource`, a custom `TemplatedControl` with a `ControlTheme`, real (headless) mouse input.
- XAML can use types from module-private assemblies in `libs/` (converter in `Helpers` -> `Humanizer`).
- The host can open module `avares://` assets before any module code has run (icons).
- Packaging: referencing Avalonia with `ExcludeAssets="runtime" PrivateAssets="all"` keeps every Avalonia
  DLL out of the zip without touching the `OpenShockHostAssembly` list. The module ALC ends up holding only
  the module, `Helpers` and `Humanizer`; Avalonia types are the host's.
- Version skew: a module built against Avalonia **12.0.0** runs on a **12.2.0** host, all checks pass
  (`dotnet publish ... -p:SpikeModuleAvaloniaVersion=12.0.0`).

## Spike 1 + 3: legacy Blazor in `NativeWebView` (`LegacyWebViewSpike`)

Can the Avalonia shell show legacy Blazor modules by pointing `NativeWebView` at an in-process
Blazor Server host on loopback?

```sh
dotnet run --project LegacyWebViewSpike -- out/webview   # opens a window for ~10s, then exits
```

**Result on Windows (WebView2): works.**

- Loopback guard: no cookie -> 401, foreign `Host` header (DNS rebinding) -> 403, wrong secret -> 401.
  The secret cookie is `HttpOnly`, so page JS cannot read it. The Blazor websocket works behind it.
- MudBlazor static assets are served, the page renders, clicks round-trip through the circuit.
- The host can navigate the legacy view with `InvokeScript("Blazor.navigateTo(...)")`, keeping the same
  circuit and page (no reload).
- Page -> host messaging works: both `invokeCSharpAction(...)` and `chrome.webview.postMessage(...)` arrive
  on `WebMessageReceived`. Host -> page goes through `InvokeScript`.

Findings that change the plan:

- **An application manifest declaring Windows 10 support is required.** Without it `NativeWebView` throws
  `Unable to create child window for native control host`. See `LegacyWebViewSpike/app.manifest`.
- **Airspace (spike 3):** an Avalonia `Popup` (its own top-level window) draws over the webview, but an
  in-window overlay does **not**: it is hidden behind the native child window. Shell dialogs/flyouts
  shown while a legacy page is open must be popups or separate windows, or the webview has to be hidden
  for the duration.
- `InvokeScript` returns JSON-encoded strings on WebView2 (`"\"0\""`); decode before comparing.
- The prerendered DOM is there before stylesheets finish loading; anything measuring layout has to wait.

Not covered yet: Linux (WebKitGTK, X11 vs Wayland) and macOS. That is the next thing to run.
