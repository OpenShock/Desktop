using System.Collections.Immutable;
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
        RootKind = ModulePageKind.Blazor,
        NavigationItems = []
    };

    public required IconOneOf? Icon { get; init; }

    /// <summary>A Blazor component or an Avalonia control, see <see cref="RootKind"/>.</summary>
    public required Type? RootComponent { get; init; }

    public required ModulePageKind RootKind { get; init; }

    public required ImmutableArray<ModuleNavigationEntry> NavigationItems { get; init; }

    /// <summary>
    /// Reads the module's UI surface, substituting a safe default for anything it cannot provide.
    /// </summary>
    public static ModuleUiSurface Capture(DesktopModuleBase module, string moduleId, ILogger logger)
    {
        var root = CaptureRootComponent(module, moduleId, logger);

        return new ModuleUiSurface
        {
            Icon = CaptureIcon(module, moduleId, logger),
            RootComponent = root?.Type,
            RootKind = root?.Kind ?? ModulePageKind.Blazor,
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

    private static (Type Type, ModulePageKind Kind)? CaptureRootComponent(DesktopModuleBase module, string moduleId,
        ILogger logger)
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

        if (ModulePageTypes.Classify(rootComponent) is not { } kind)
        {
            logger.LogError(
                "Module {ModuleId} declares root component {Type}, which is neither a blazor component nor an Avalonia control",
                moduleId, rootComponent.FullName);
            return null;
        }

        return (rootComponent, kind);
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

            if (navigationComponent.ComponentType is null ||
                ModulePageTypes.Classify(navigationComponent.ComponentType) is not { } kind)
            {
                logger.LogError(
                    "Navigation component {Name} of module {ModuleId} points at {Type}, which is neither a blazor component nor an Avalonia control, skipping it",
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
                navigationComponent.Icon, kind));
        }

        return entries.ToImmutable();
    }
}

/// <summary>
/// A validated <see cref="NavigationItem"/>.
/// </summary>
/// <param name="ComponentType">A Blazor component or an Avalonia control, see <paramref name="Kind"/>.</param>
public sealed record ModuleNavigationEntry(string Name, Type ComponentType, IconOneOf? Icon, ModulePageKind Kind);
