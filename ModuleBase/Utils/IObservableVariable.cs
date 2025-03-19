using OpenShock.MinimalEvents;

namespace OpenShock.Desktop.ModuleBase.Utils;

public interface IObservableVariable<out T>
{
    public IAsyncMinimalEventObservable<T> ValueUpdated { get; }
    public T Value { get; }
}