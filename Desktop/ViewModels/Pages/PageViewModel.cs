using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Ui;

namespace OpenShock.Desktop.ViewModels.Pages;

/// <summary>A built-in page. Created when it is navigated to and disposed when it is left.</summary>
public abstract class PageViewModel : ViewModelBase
{
    protected PageViewModel(PageContext context)
    {
        Context = context;
    }

    protected PageContext Context { get; }

    protected T Get<T>() where T : notnull => Context.Services.GetRequiredService<T>();
}

/// <summary>What a page can reach of the shell around it.</summary>
public sealed class PageContext
{
    private readonly AppNavigator _navigator;
    private readonly Notifier _notifier;

    public PageContext(IServiceProvider services, AppNavigator navigator, Notifier notifier, IDialogHost dialogs)
    {
        Services = services;
        _navigator = navigator;
        _notifier = notifier;
        Dialogs = dialogs;
    }

    public IServiceProvider Services { get; }
    public IDialogHost Dialogs { get; }

    public void Navigate(string route) => _navigator.Navigate(route);

    public void Notify(string message, UiNotificationSeverity severity = UiNotificationSeverity.Info) =>
        _notifier.Show(message, severity);
}

/// <summary>Dialogs, which are separate windows owned by the main window. Implemented by the main window.</summary>
public interface IDialogHost
{
    Task<bool> ConfirmAsync(ConfirmRequest request);

    void ShowUpdateDialog();
}

/// <param name="Warning">An extra line in the warning colour, for what could go wrong.</param>
public sealed record ConfirmRequest(
    string Title,
    string Message,
    string Confirm,
    string Cancel = "Cancel",
    string? Warning = null,
    Avalonia.Media.Geometry? Icon = null);
