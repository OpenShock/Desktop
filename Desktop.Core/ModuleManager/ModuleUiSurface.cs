using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Navigation;

namespace OpenShock.Desktop.ModuleManager;

/// <summary>
/// Everything the shell needs to render a module's navigation and pages, captured once when the
/// module is loaded.
/// </summary>
/// <remarks>
/// <see cref="DesktopModuleBase.Icon"/>, <see cref="DesktopModuleBase.RootComponent"/> and
/// <see cref="DesktopModuleBase.NavigationComponents"/> are all virtual, so reading them runs module
/// code. Reading them straight from the render tree means a module that throws from a property
/// getter, or hands back a type that is not a component, takes the exception into the renderer on
/// every single frame - and the sidebar renders on every page, so that is the whole app, not just
/// the module's own page. Capturing here turns that into one logged failure at load time.
/// </remarks>
public sealed class ModuleUiSurface
{
    public static ModuleUiSurface Empty { get; } = new()
    {
        Icon = null,
        RootComponent = null,
        NavigationItems = []
    };

    public required IconOneOf? Icon { get; init; }

    public required Type? RootComponent { get; init; }

    public required ImmutableArray<ModuleNavigationEntry> NavigationItems { get; init; }

    /// <summary>
    /// Reads the module's UI surface, substituting a safe default for anything it cannot provide.
    /// </summary>
    public static ModuleUiSurface Capture(DesktopModuleBase module, string moduleId, ILogger logger)
    {
        return new ModuleUiSurface
        {
            Icon = CaptureIcon(module, moduleId, logger),
            RootComponent = CaptureRootComponent(module, moduleId, logger),
            NavigationItems = CaptureNavigationItems(module, moduleId, logger)
        };
    }

    private static IconOneOf? CaptureIcon(DesktopModuleBase module, string moduleId, ILogger logger)
    {
        try
        {
            return module.Icon;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Module {ModuleId} threw while reading its icon, rendering without one", moduleId);
            return null;
        }
    }

    private static Type? CaptureRootComponent(DesktopModuleBase module, string moduleId, ILogger logger)
    {
        Type? rootComponent;

        try
        {
            rootComponent = module.RootComponent;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Module {ModuleId} threw while reading its root component", moduleId);
            return null;
        }

        if (rootComponent is null) return null;

        if (!IsComponent(rootComponent))
        {
            logger.LogError("Module {ModuleId} declares root component {Type}, which is not a blazor component",
                moduleId, rootComponent.FullName);
            return null;
        }

        return rootComponent;
    }

    private static ImmutableArray<ModuleNavigationEntry> CaptureNavigationItems(DesktopModuleBase module,
        string moduleId, ILogger logger)
    {
        IReadOnlyCollection<NavigationItem>? navigationComponents;

        try
        {
            navigationComponents = module.NavigationComponents;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Module {ModuleId} threw while reading its navigation components", moduleId);
            return [];
        }

        if (navigationComponents is null) return [];

        var entries = ImmutableArray.CreateBuilder<ModuleNavigationEntry>(navigationComponents.Count);
        var seenNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        foreach (var navigationComponent in navigationComponents)
        {
            if (navigationComponent is null)
            {
                logger.LogError("Module {ModuleId} has a null navigation component, skipping it", moduleId);
                continue;
            }

            if (string.IsNullOrWhiteSpace(navigationComponent.Name))
            {
                logger.LogError("Module {ModuleId} has a navigation component without a name, skipping it", moduleId);
                continue;
            }

            if (navigationComponent.ComponentType is null || !IsComponent(navigationComponent.ComponentType))
            {
                logger.LogError(
                    "Navigation component {Name} of module {ModuleId} points at {Type}, which is not a blazor component, skipping it",
                    navigationComponent.Name, moduleId, navigationComponent.ComponentType?.FullName ?? "null");
                continue;
            }

            // The route only carries the name, so a duplicate would be unreachable anyway.
            if (!seenNames.Add(navigationComponent.Name))
            {
                logger.LogError("Module {ModuleId} has more than one navigation component named {Name}, skipping the duplicate",
                    moduleId, navigationComponent.Name);
                continue;
            }

            entries.Add(new ModuleNavigationEntry(navigationComponent.Name, navigationComponent.ComponentType,
                navigationComponent.Icon));
        }

        return entries.ToImmutable();
    }

    private static bool IsComponent(Type type) =>
        typeof(IComponent).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false };
}

/// <summary>
/// A validated <see cref="NavigationItem"/>.
/// </summary>
public sealed record ModuleNavigationEntry(string Name, Type ComponentType, IconOneOf? Icon);
