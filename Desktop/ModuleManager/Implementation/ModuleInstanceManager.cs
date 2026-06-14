using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.Services;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public class ModuleInstanceManager : IModuleInstanceManager
{
    private readonly LoadedModule _loadedModule;
    private readonly ILoggerFactory _loggerFactory;

    public ModuleInstanceManager(
        LoadedModule loadedModule, 
        ILoggerFactory loggerFactory, 
        IServiceProvider serviceProvider)
    {
        _loadedModule = loadedModule;
        _loggerFactory = loggerFactory;

        OpenShock = new OpenShockService(serviceProvider);
        
        ModuleDataDirectory = Path.Combine(Constants.ModuleData, _loadedModule.Id);
        
        if(!Directory.Exists(ModuleDataDirectory)) Directory.CreateDirectory(ModuleDataDirectory);
    }
    
    public async Task<IModuleConfig<T>> GetModuleConfig<T>() where T : new()
    {
        var moduleConfigFile = Path.Combine(ModuleDataDirectory, "config.json");

        return await ModuleConfig<T>.Create(moduleConfigFile,
            _loggerFactory.CreateLogger("ModuleConfig+" + _loadedModule.Id));
    }

    public required IServiceProvider AppServiceProvider { get; init; }

    public IOpenShockService OpenShock { get; }
    
    public string ModuleDataDirectory { get; init; }
}