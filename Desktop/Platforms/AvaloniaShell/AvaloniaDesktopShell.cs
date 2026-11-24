using OpenShock.Desktop.Services;

namespace OpenShock.Desktop.Platforms.AvaloniaShell;

/// <summary>Exposes <see cref="Shell"/> to code that only knows Core.</summary>
public sealed class AvaloniaDesktopShell : IDesktopShell
{
    public void ShowMainWindow() => Shell.ShowMainWindow();

    public void Quit() => Shell.Quit();
}
