using OpenShock.Desktop.ModuleBase.Config;

namespace OpenShock.Desktop.ModuleBase;

public interface IModuleInstanceManager
{
    public Task<IModuleConfig<T>> GetModuleConfig<T>() where T : new();
    public IServiceProvider ServiceProvider { get; }
}