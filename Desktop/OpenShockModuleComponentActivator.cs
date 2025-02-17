using Microsoft.AspNetCore.Components;

namespace OpenShock.Desktop;

public class OpenShockModuleComponentActivator : IComponentActivator
{
    private readonly IServiceProvider _defaultProvider;

    public OpenShockModuleComponentActivator(IServiceProvider defaultProvider)
    {
        _defaultProvider = defaultProvider;
    }

    public IComponent CreateInstance(Type componentType)
    {
        // Determine if this component should be created with a module-specific provider.
        if (typeof(IModuleComponent).IsAssignableFrom(componentType))
        {
            // For example, look up a module-specific provider using a custom mechanism.
            // You might have a module manager that tracks service providers per module.
            var moduleProvider = ModuleManager.GetModuleServiceProvider(componentType);
            if (moduleProvider != null)
            {
                // Use the module-specific provider to create the component.
                return (IComponent)ActivatorUtilities.CreateInstance(moduleProvider, componentType);
            }
        }

        // Fallback: use the default (host) service provider.
        return (IComponent)ActivatorUtilities.CreateInstance(_defaultProvider, componentType);
    }
}