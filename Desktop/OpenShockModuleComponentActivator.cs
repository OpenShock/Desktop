using System.Reflection;
using Microsoft.AspNetCore.Components;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleManager;

namespace OpenShock.Desktop;

public class OpenShockModuleComponentActivator : IComponentActivator
{
    private readonly IServiceProvider _defaultProvider;
    private readonly ModuleManager.ModuleManager _moduleManager;
    private readonly ILogger<OpenShockModuleComponentActivator> _logger;

    public OpenShockModuleComponentActivator(IServiceProvider defaultProvider, ModuleManager.ModuleManager moduleManager, ILogger<OpenShockModuleComponentActivator> logger)
    {
        _defaultProvider = defaultProvider;
        _moduleManager = moduleManager;
        _logger = logger;
    }

    public IComponent CreateInstance(Type componentType)
    {
        
        var module = _moduleManager.Modules.Where(x => x.Value.Assembly == componentType.Assembly).Select(x => x.Value).FirstOrDefault();
        if (module != null)
        {
            var componentObject = ActivatorUtilities.CreateInstance(_defaultProvider, componentType);
            var component = (IComponent)componentObject;
            
            InjectModuleDependencies(componentObject, componentType, module);
            
            return component;
        }
        
        return (IComponent)ActivatorUtilities.CreateInstance(_defaultProvider, componentType);
    }

    private void InjectModuleDependencies(object instance, Type componentType, LoadedModule module)
    {
        var props = componentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(prop => prop.IsDefined(typeof(ModuleInjectAttribute), inherit: true) && prop.CanWrite);

        foreach (var prop in props)
        {
            // Only set the property if it hasn't been set already
            if (prop.GetValue(instance) is not null)
            {
                _logger.LogWarning(
                    "Property {PropertyName} on component {ComponentType} has already been set, skipping injection",
                    prop.Name, componentType.Name);
                return;
            }

            var service = module.Module.ModuleServiceProvider.GetService(prop.PropertyType);
            if (service == null)
            {
                throw new Exception(
                    $"There is no registered service of type {prop.PropertyType.Name} for module {module.Module.Id}");
            }

            prop.SetValue(instance, service);
        }
    }
}