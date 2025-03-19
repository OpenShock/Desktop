using System.Reactive.Subjects;
using OpenShock.Desktop.ModuleBase.Utils;
using OpenShock.MinimalEvents;

namespace OpenShock.Desktop.Utils;

public sealed class ObservableVariable<T> : IObservableVariable<T>
{
    public IAsyncMinimalEventObservable<T> ValueUpdated => _subject;
    private readonly AsyncMinimalEvent<T> _subject = new();
    
    private T _value;
    
    public T Value
    {
        get => _value;
        set
        {
            _value = value; 
            OsTask.Run(async () => { await _subject.InvokeAsyncParallel(value); });
        }
    }
    
    public ObservableVariable(T initialValue)
    {
        _value = initialValue;
        
    }
}