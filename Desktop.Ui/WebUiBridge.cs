namespace OpenShock.Desktop.Ui;

public enum UiNotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Connects the native shell with the Blazor UI shown in its webview. Both run in this process, so this is a plain
/// singleton rather than anything going through JavaScript.
/// </summary>
/// <remarks>
/// The Blazor side (<see cref="Main"/>) attaches once its circuit is up and reports every location change. The shell
/// navigates and shows messages through it. Anything asked before a circuit is attached is held and delivered on
/// attach, so the shell never has to care whether the webview has finished loading.
/// </remarks>
public sealed class WebUiBridge
{
    private readonly Lock _lock = new();
    private Registration? _current;
    private string? _pendingNavigation;
    private readonly List<(string Message, UiNotificationSeverity Severity)> _pendingNotifications = [];

    /// <summary>The base-relative path the Blazor UI is on, starting with a slash and without query or fragment.</summary>
    public string CurrentPath { get; private set; } = "/";

    /// <summary>Raised with the new <see cref="CurrentPath"/>, on whatever thread Blazor navigated on.</summary>
    public event Action<string>? LocationChanged;

    /// <summary>Navigates the Blazor UI to <paramref name="path"/>.</summary>
    public void Navigate(string path)
    {
        Registration? registration;
        lock (_lock)
        {
            registration = _current;
            if (registration is null)
            {
                _pendingNavigation = path;
                return;
            }
        }

        registration.Navigate(path);
    }

    /// <summary>Shows a message the way the Blazor UI shows its own (a snackbar).</summary>
    public void Notify(string message, UiNotificationSeverity severity)
    {
        Registration? registration;
        lock (_lock)
        {
            registration = _current;
            if (registration is null)
            {
                _pendingNotifications.Add((message, severity));
                return;
            }
        }

        registration.Notify(message, severity);
    }

    /// <summary>Called by the Blazor UI once it can navigate and show messages. Dispose to detach.</summary>
    internal IDisposable Attach(Action<string> navigate, Action<string, UiNotificationSeverity> notify)
    {
        var registration = new Registration(this, navigate, notify);

        string? pendingNavigation;
        (string Message, UiNotificationSeverity Severity)[] pendingNotifications;
        lock (_lock)
        {
            _current = registration;
            pendingNavigation = _pendingNavigation;
            _pendingNavigation = null;
            pendingNotifications = [.. _pendingNotifications];
            _pendingNotifications.Clear();
        }

        if (pendingNavigation is not null) navigate(pendingNavigation);
        foreach (var (message, severity) in pendingNotifications) notify(message, severity);

        return registration;
    }

    internal void ReportLocation(string path)
    {
        CurrentPath = path;
        LocationChanged?.Invoke(path);
    }

    private sealed class Registration(
        WebUiBridge bridge,
        Action<string> navigate,
        Action<string, UiNotificationSeverity> notify) : IDisposable
    {
        public void Navigate(string path) => navigate(path);

        public void Notify(string message, UiNotificationSeverity severity) => notify(message, severity);

        public void Dispose()
        {
            // A reload attaches the new circuit before the old one is torn down, so only detach ourselves.
            lock (bridge._lock)
            {
                if (ReferenceEquals(bridge._current, this)) bridge._current = null;
            }
        }
    }
}
