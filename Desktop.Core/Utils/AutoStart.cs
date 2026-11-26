using System.Diagnostics;

namespace OpenShock.Desktop.Utils;

/// <summary>
/// Starting the app with Windows, or with VRChat through VRCX's app launcher. Both are a shortcut to this exe in a
/// folder that gets launched: the user's Startup folder, or VRCX's startup folder.
/// </summary>
public static class AutoStart
{
    private static string ShortcutName => Path.GetFileName(Environment.ProcessPath!) + ".lnk";

    private static string VrcxStartupFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCX", "startup");

    /// <summary>Adds or removes the shortcut in the Windows Startup folder.</summary>
    public static void SetWindows(bool enabled) =>
        SetShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName), enabled);

    /// <summary>Adds or removes the shortcut in VRCX's startup folder. Does nothing without VRCX.</summary>
    public static void SetVrcx(bool enabled)
    {
        if (!Directory.Exists(VrcxStartupFolder)) return;
        SetShortcut(Path.Combine(VrcxStartupFolder, ShortcutName), enabled);
    }

    private static void SetShortcut(string shortcutPath, bool enabled)
    {
        if (!enabled)
        {
            File.Delete(shortcutPath);
            return;
        }

        if (!OperatingSystem.IsWindows()) return;

        var exePath = Environment.ProcessPath!;
        // Single quoted PowerShell strings only need their quotes doubled.
        static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

        var script = "$WshShell = New-Object -ComObject WScript.Shell; " +
                     $"$Shortcut = $WshShell.CreateShortcut({Quote(shortcutPath)}); " +
                     $"$Shortcut.TargetPath = {Quote(exePath)}; " +
                     $"$Shortcut.WorkingDirectory = {Quote(Path.GetDirectoryName(exePath)!)}; " +
                     "$Shortcut.Save()";

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{script}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        });
    }
}
