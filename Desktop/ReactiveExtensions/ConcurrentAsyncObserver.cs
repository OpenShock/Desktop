namespace OpenShock.Desktop.ReactiveExtensions;

public class ConcurrentAsyncObserver<T> : IAsyncObserver<T>
{
    private readonly Func<T, ValueTask> _onNextAsync;
    private readonly Func<Exception, ValueTask> _onErrorAsync;
    private readonly Func<ValueTask> _onCompletedAsync;

    public ConcurrentAsyncObserver(Func<T, ValueTask> onNextAsync, Func<Exception, ValueTask> onErrorAsync, Func<ValueTask> onCompletedAsync)
    {
        _onNextAsync = onNextAsync ?? throw new ArgumentNullException(nameof(onNextAsync));
        _onErrorAsync = onErrorAsync ?? throw new ArgumentNullException(nameof(onErrorAsync));
        _onCompletedAsync = onCompletedAsync ?? throw new ArgumentNullException(nameof(onCompletedAsync));
    }

    public ValueTask OnCompletedAsync() => _onCompletedAsync();

    public ValueTask OnErrorAsync(Exception error) => _onErrorAsync(error ?? throw new ArgumentNullException(nameof(error)));

    public ValueTask OnNextAsync(T value) => _onNextAsync(value);
}