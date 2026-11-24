using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenShock.Desktop.ViewModels;

/// <summary>
/// Core services raise their events on whatever thread they like; view models only ever touch their state on the
/// UI thread (<see cref="OnUi"/>) and drop their subscriptions when disposed.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IAsyncDisposable
{
    private readonly List<Task<IAsyncDisposable>> _asyncSubscriptions = [];
    private readonly List<IDisposable> _subscriptions = [];
    private bool _disposed;

    protected static void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread, for use as an async event handler.</summary>
    protected static Task OnUiAsync(Action action)
    {
        OnUi(action);
        return Task.CompletedTask;
    }

    protected void Track(ValueTask<IAsyncDisposable> subscription) => _asyncSubscriptions.Add(subscription.AsTask());

    protected void Track(IDisposable subscription) => _subscriptions.Add(subscription);

    public virtual async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var subscription in _subscriptions) subscription.Dispose();
        foreach (var subscription in _asyncSubscriptions) await (await subscription).DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
