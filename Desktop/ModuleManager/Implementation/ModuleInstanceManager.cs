﻿using OpenShock.Desktop.Backend;
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
        BackendHubManager backendHubManager,
        LiveControlManager liveControlManager,
        OpenShockApi openShockApi)
    {
        _loadedModule = loadedModule;
        _loggerFactory = loggerFactory;

        OpenShock = new OpenShockService(backendHubManager, liveControlManager, openShockApi);
    }
    
    public async Task<IModuleConfig<T>> GetModuleConfig<T>() where T : new()
    {
        var moduleConfigPath = Path.Combine(Constants.ModuleData, _loadedModule.Module.Id);
        
        if(!Directory.Exists(moduleConfigPath)) Directory.CreateDirectory(moduleConfigPath);
        
        var moduleConfigFile = Path.Combine(moduleConfigPath, "config.json");

        return await ModuleConfig<T>.Create(moduleConfigFile,
            _loggerFactory.CreateLogger("ModuleConfig+" + _loadedModule.Module.Id));
    }

    public required IServiceProvider AppServiceProvider { get; init; }

    public IOpenShockService OpenShock { get; }
}