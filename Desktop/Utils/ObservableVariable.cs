using System.Reactive.Subjects;
using OpenShock.Desktop.ModuleBase.Utils;

namespace OpenShock.Desktop.Utils;

public class ObservableVariable<T> : IObservableVariable<T>
{
    public IAsyncObservable<T> ValueUpdated => _subject;
    private readonly ConcurrentSimpleAsyncSubject<T> _subject = new ConcurrentSimpleAsyncSubject<T>();
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    
    private T _value;
    
    public T Value
    {
        get => _value;
        set
        {
            _value = value; 
            OsTask.Run(async () =>
            {
                await _semaphore.WaitAsync();

                try
                {
                    await _subject.OnNextAsync(value);
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }
    }
    
    public ObservableVariable(T initialValue)
    {
        _value = initialValue;
        
    }
    
    internal ValueTask OnCompletedAsync() => _subject.OnCompletedAsync();
}