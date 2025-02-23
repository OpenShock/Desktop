namespace OpenShock.Desktop.ModuleBase.Utils;

public interface IObservableVariable<T>
{
    public IAsyncObservable<T> ValueUpdated { get; }
    public T Value { get; }
}