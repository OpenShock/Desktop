﻿using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using ModuleBase;

namespace OpenShock.Desktop.ModuleManager;

public sealed class ModuleManager
{
    private static readonly Type ModuleType = typeof(IModule);
    
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleManager> _logger;
    
    private static string ModuleDirectory => Path.Combine(Constants.AppdataFolder, "modules");

    public ModuleManager(IServiceProvider serviceProvider, ILogger<ModuleManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public readonly ConcurrentDictionary<string, LoadedModule> Modules = new();

    private void LoadModule(string moduleFolderPath)
    {
        _logger.LogTrace("Searching for module in {Path}", moduleFolderPath);
        var moduleFile = Directory.GetFiles(moduleFolderPath, "*.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if(moduleFile == null)
        {
            _logger.LogWarning("No DLLs found in root module folder!");
            return;
        }
        _logger.LogDebug("Attempting to load module: {Path}", moduleFile);

        var assemblyLoadContext = new ModuleAssemblyLoadContext(moduleFolderPath);
        var assembly = assemblyLoadContext.LoadFromAssemblyPath(moduleFile);
        
        var pluginTypes = assembly.GetTypes().Where(t => t.IsAssignableTo(ModuleType)).ToArray();
        switch (pluginTypes.Length)
        {
            case 0:
                _logger.LogWarning("No modules found in DLL!");
                return;
            case > 1:
                _logger.LogWarning("Expected 1 module, found {NumberOfModules}", pluginTypes.Length);
                return;
        }

        var module = (IModule?)ActivatorUtilities.CreateInstance(_serviceProvider, pluginTypes[0]);
        if (module is null) throw new Exception("Failed to instantiate module!");
        
        var loadedModule = new LoadedModule
        {
            LoadContext = assemblyLoadContext,
            Assembly = assembly,
            Module = module
        };

        if (!Modules.TryAdd(module.Id, loadedModule)) throw new Exception("Module already loaded!");
    }

    internal void LoadAll()
    {
        if (!Directory.Exists(ModuleDirectory))
        {
            Directory.CreateDirectory(ModuleDirectory);
            return; // We don't have any modules to load, we just created the directory
        }

        foreach (var moduleFolders in Directory.GetDirectories(ModuleDirectory))
        {
            try
            {
                LoadModule(moduleFolders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin");
            }
        }
    }
}
