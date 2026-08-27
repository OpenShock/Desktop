using System.Reflection;
using Microsoft.AspNetCore.Components;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleManager;
using OpenShock.Desktop.Ui.ErrorHandling;

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
        var module = FindOwningModule(componentType);

        // Not a module component. Our own bugs should keep surfacing as exceptions.
        if (module is null) return (IComponent)ActivatorUtilities.CreateInstance(_defaultProvider, componentType);

        try
        {
            var componentObject = ActivatorUtilities.CreateInstance(_defaultProvider, componentType);

            InjectModuleDependencies(componentObject, componentType, module);

            return (IComponent)componentObject;
        }
        catch (Exception e)
        {
            // This runs inside the renderer, mid diff. Throwing here hands the exception to whatever
            // error boundary happens to be above us, which in practice means a broken module wipes
            // out the page it was rendered on. Standing in a placeholder keeps the failure the size
            // of the component that actually failed.
            _logger.LogError(e, "Failed to create component {ComponentType} of module {ModuleId}", componentType.FullName,
                module.Id);

            return ModuleComponentFailed.Create(module, componentType, e);
        }
    }

    private LoadedModule? FindOwningModule(Type componentType) => _moduleManager.Modules
        .Where(x => x.Value.Assembly == componentType.Assembly)
        .Select(x => x.Value)
        .FirstOrDefault();

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
                continue;
            }

            var service = module.Module.ModuleServiceProvider.GetService(prop.PropertyType);
            if (service == null)
            {
                throw new Exception(
                    $"There is no registered service of type {prop.PropertyType.Name} for module {module.Id}");
            }

            prop.SetValue(instance, service);
        }
    }
}
