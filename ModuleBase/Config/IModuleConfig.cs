namespace OpenShock.Desktop.ModuleBase.Config;

public interface IModuleConfig<T>
{
    public T Config { get; }
    public void SaveDeferred();
    public Task Save();
}