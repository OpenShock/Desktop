using System.Reactive.Subjects;
using OpenShock.Desktop.ModuleBase.Utils;

namespace OpenShock.Desktop.Utils;

public class ObservableVariable<T> : IObservableVariable<T>
{
    public IAsyncObservable<T> ValueUpdated => _subject;
    private readonly ConcurrentSimpleAsyncSubject<T> _subject = new();
    
    private T _value;
    
    public T Value
    {
        get => _value;
        set
        {
            _value = value; 
            OsTask.Run(async () => { await _subject.OnNextAsync(value); });
        }
    }
    
    public ObservableVariable(T initialValue)
    {
        _value = initialValue;
        
    }
    
    internal ValueTask OnCompletedAsync() => _subject.OnCompletedAsync();
}