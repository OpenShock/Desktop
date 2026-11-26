using Microsoft.AspNetCore.Components;

namespace OpenShock.Desktop.ModuleManager;

/// <summary>How a module page is drawn.</summary>
public enum ModulePageKind
{
    /// <summary>A Blazor component: a legacy page, shown in the webview.</summary>
    Blazor,

    /// <summary>An Avalonia control, shown natively.</summary>
    Native
}

/// <summary>
/// Tells the two kinds of module page apart. A module page type is a Blazor component (the original API) or an
/// Avalonia control (the native API); anything else cannot be shown.
/// </summary>
public static class ModulePageTypes
{
    /// <summary>
    /// The base of every Avalonia control. Matched by name, so Core (which headless mode runs on) does not need
    /// Avalonia: the host provides the one Avalonia every module shares, so the name is unambiguous.
    /// </summary>
    public const string AvaloniaControlTypeName = "Avalonia.Controls.Control";

    /// <summary>The kind of page <paramref name="type"/> is, or null when it is neither.</summary>
    public static ModulePageKind? Classify(Type type)
    {
        if (type is { IsAbstract: true } or { IsInterface: true } or { ContainsGenericParameters: true }) return null;

        if (typeof(IComponent).IsAssignableFrom(type)) return ModulePageKind.Blazor;

        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.FullName == AvaloniaControlTypeName) return ModulePageKind.Native;

        return null;
    }
}
