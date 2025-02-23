using OpenShock.Desktop.ModuleBase.Config;

namespace OpenShock.Desktop.ModuleBase.Api;

public interface IModuleInstanceManager
{
    public Task<IModuleConfig<T>> GetModuleConfig<T>() where T : new();
    public IServiceProvider AppServiceProvider { get; }
    
    public IOpenShockService OpenShock { get; }
}