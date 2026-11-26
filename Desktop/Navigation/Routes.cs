namespace OpenShock.Desktop.Navigation;

/// <summary>
/// The app's routes. They keep the paths the Blazor UI used, so a module navigating to one of them (it can only do
/// that through Blazor's NavigationManager) still lands on the right page.
/// </summary>
public static class Routes
{
    public const string Dashboard = "/dash/dashboard";
    public const string Shockers = "/dash/shockers";
    public const string Modules = "/dash/modules";
    public const string ModulesAvailable = "/dash/modules?tab=available";
    public const string Settings = "/dash/settings";
    public const string Logs = "/dash/logs";
    public const string Debug = "/dash/debug";
    public const string Login = "/dash/login";

    /// <summary>Module pages. These are the only ones still drawn by the Blazor UI, in the webview.</summary>
    public const string ModulePrefix = "/dash/module";

    public static string Module(string moduleId, string? page = null) =>
        page is null ? $"{ModulePrefix}/{moduleId}" : $"{ModulePrefix}/{moduleId}/{Uri.EscapeDataString(page)}";

    /// <summary>The path of <paramref name="route"/>: leading slash, no trailing slash, no query or fragment.</summary>
    public static string PathOf(string route) => "/" + route.Split('?', '#')[0].Trim('/');

    /// <summary>The value of <paramref name="name"/> in the query of <paramref name="route"/>, if there is one.</summary>
    public static string? QueryValue(string route, string name)
    {
        var queryStart = route.IndexOf('?');
        if (queryStart < 0) return null;

        foreach (var pair in route[(queryStart + 1)..].Split('#')[0].Split('&'))
        {
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair[..separator];
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
                return separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        return null;
    }

    public static bool IsModulePage(string path) => Matches(path, ModulePrefix);

    /// <summary>Whether <paramref name="currentPath"/> is <paramref name="route"/> or below it, on segment boundaries.</summary>
    public static bool Matches(string currentPath, string route, bool exact = false)
    {
        var current = Uri.UnescapeDataString(PathOf(currentPath)).Trim('/');
        var target = Uri.UnescapeDataString(PathOf(route)).Trim('/');

        return current.Equals(target, StringComparison.OrdinalIgnoreCase) ||
               (!exact && current.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase));
    }
}
