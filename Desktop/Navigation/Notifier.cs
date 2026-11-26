using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.Theming;
using OpenShock.Desktop.Ui;

namespace OpenShock.Desktop.Navigation;

/// <summary>
/// Short messages in the corner of the page, as MudBlazor's snackbars. Over a module page they go to the Blazor UI
/// instead: anything the window draws itself would sit behind the webview there.
/// </summary>
public sealed class Notifier
{
    private const int MaxToasts = 5;
    private static readonly TimeSpan VisibleFor = TimeSpan.FromSeconds(5);

    private readonly AppNavigator _navigator;
    private readonly WebUiBridge _bridge;

    public Notifier(AppNavigator navigator, WebUiBridge bridge)
    {
        _navigator = navigator;
        _bridge = bridge;
    }

    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    /// <summary>Shows <paramref name="message"/>. Safe to call from any thread.</summary>
    public void Show(string message, UiNotificationSeverity severity = UiNotificationSeverity.Info) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_navigator.IsWebViewActive)
            {
                _bridge.Notify(message, severity);
                return;
            }

            var toast = new ToastViewModel(message, severity, Dismiss);
            Toasts.Add(toast);
            while (Toasts.Count > MaxToasts) Toasts.RemoveAt(0);

            DispatcherTimer.RunOnce(() => Dismiss(toast), VisibleFor);
        });

    private void Dismiss(ToastViewModel toast) => Toasts.Remove(toast);
}

public sealed partial class ToastViewModel
{
    private readonly Action<ToastViewModel> _dismiss;

    public ToastViewModel(string message, UiNotificationSeverity severity, Action<ToastViewModel> dismiss)
    {
        Message = message;
        Severity = severity;
        _dismiss = dismiss;
    }

    public string Message { get; }
    public UiNotificationSeverity Severity { get; }

    public IBrush Background => Severity switch
    {
        UiNotificationSeverity.Success => ThemeBrushes.Success,
        UiNotificationSeverity.Warning => ThemeBrushes.Warning,
        UiNotificationSeverity.Error => ThemeBrushes.Error,
        _ => ThemeBrushes.Info
    };

    public Geometry Icon => Severity switch
    {
        UiNotificationSeverity.Success => MaterialIcons.CheckCircle,
        UiNotificationSeverity.Warning => MaterialIcons.Warning,
        UiNotificationSeverity.Error => MaterialIcons.Error,
        _ => MaterialIcons.Info
    };

    [RelayCommand]
    private void Dismiss() => _dismiss(this);
}
