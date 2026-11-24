namespace OpenShock.Desktop.Services;

/// <summary>
/// The window and lifetime of whatever hosts the app, for code outside the host that needs to show the window
/// or quit (restart after a module install, for example). Not registered in headless mode, where there is no
/// window to show.
/// </summary>
public interface IDesktopShell
{
    /// <summary>Brings the main window to the front, restoring it from the tray if needed.</summary>
    void ShowMainWindow();

    /// <summary>Shuts the app down normally, as if the user had quit it.</summary>
    void Quit();
}
