using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Config;

namespace OpenShock.Desktop.ModuleManager;

public class ModuleInstanceManager : IModuleInstanceManager
{
    private readonly LoadedModule _loadedModule;
    private readonly ILoggerFactory _loggerFactory;
    
    public ModuleInstanceManager(LoadedModule loadedModule, ILoggerFactory loggerFactory)
    {
        _loadedModule = loadedModule;
        _loggerFactory = loggerFactory;
    }
    
    public async Task<IModuleConfig<T>> GetModuleConfig<T>() where T : new()
    {
        var moduleConfigPath = Path.Combine(Constants.ModuleData, _loadedModule.Module.Id);
        
        if(!Directory.Exists(moduleConfigPath)) Directory.CreateDirectory(moduleConfigPath);
        
        var moduleConfigFile = Path.Combine(moduleConfigPath, "config.json");

        return await ModuleConfig<T>.Create(moduleConfigFile,
            _loggerFactory.CreateLogger("ModuleConfig+" + _loadedModule.Module.Id));
    }

    public required IServiceProvider ServiceProvider { get; init; }
}