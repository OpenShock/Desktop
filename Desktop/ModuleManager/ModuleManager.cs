using System.Collections.Concurrent;
using System.Reflection;
using ModuleBase;

namespace OpenShock.Desktop.ModuleManager;

public sealed class ModuleManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleManager> _logger;

    public ModuleManager(IServiceProvider serviceProvider, ILogger<ModuleManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public readonly ConcurrentDictionary<string, LoadedModule> Modules = new();

    private void LoadPlugin(string assemblyPath)
    {
        var assemblyAbsolutePath = Path.Combine(Directory.GetCurrentDirectory(), assemblyPath);
        _logger.LogDebug("Attempting to load plugin: {Path}", assemblyAbsolutePath);
        var assembly = Assembly.LoadFile(assemblyAbsolutePath);

        var moduleType = typeof(IModule);
        var typesInAssembly = assembly.GetTypes();
        var pluginTypes = typesInAssembly.Where(t => t.IsAssignableTo(moduleType)).ToArray();
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
            Assembly = assembly,
            Module = module
        };

        if (!Modules.TryAdd(module.Id, loadedModule)) throw new Exception("Module already loaded!");
    }

    internal void LoadAll()
    {
        if (!Directory.Exists("Modules")) return;

        foreach (var pluginPath in Directory.GetFiles("Modules", "*.dll"))
        {
            try
            {
                LoadPlugin(pluginPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin");
            }
        }
    }
}
