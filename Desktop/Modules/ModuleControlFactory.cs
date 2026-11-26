using System.Reflection;
using Avalonia.Controls;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleManager;

namespace OpenShock.Desktop.Modules;

/// <summary>
/// Builds a module's native page, the Avalonia counterpart of the Blazor OpenShockModuleComponentActivator.
/// </summary>
/// <remarks>
/// Constructor parameters are resolved from the module's services first, then the app's. Properties marked
/// <see cref="ModuleInjectAttribute"/> are filled from the module's services afterwards, as for Blazor pages, so a
/// module can share one page class between both.
/// </remarks>
public static class ModuleControlFactory
{
    public static Control Create(LoadedModule module, Type controlType, IServiceProvider appServices)
    {
        var services = new ModuleFirstServiceProvider(module.Module.ModuleServiceProvider, appServices);
        var instance = ActivatorUtilities.CreateInstance(services, controlType);

        InjectModuleDependencies(instance, controlType, module);

        return (Control)instance;
    }

    private static void InjectModuleDependencies(object instance, Type controlType, LoadedModule module)
    {
        var properties = controlType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(x => x.IsDefined(typeof(ModuleInjectAttribute), inherit: true) && x.CanWrite);

        foreach (var property in properties)
        {
            // Set by the constructor already.
            if (property.GetValue(instance) is not null) continue;

            var service = module.Module.ModuleServiceProvider.GetService(property.PropertyType) ??
                          throw new InvalidOperationException(
                              $"There is no registered service of type {property.PropertyType.Name} for module {module.Id}");

            property.SetValue(instance, service);
        }
    }

    /// <remarks>
    /// Implements <see cref="IServiceProviderIsService"/> as well, which <see cref="ActivatorUtilities"/> uses to pick a
    /// constructor; without it, a constructor with service parameters cannot be chosen.
    /// </remarks>
    private sealed class ModuleFirstServiceProvider(IServiceProvider module, IServiceProvider app)
        : IServiceProvider, IServiceProviderIsService
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceProviderIsService)) return this;
            return module.GetService(serviceType) ?? app.GetService(serviceType);
        }

        public bool IsService(Type serviceType) => IsService(module, serviceType) || IsService(app, serviceType);

        private static bool IsService(IServiceProvider provider, Type serviceType) =>
            provider.GetService(typeof(IServiceProviderIsService)) is IServiceProviderIsService isService
                ? isService.IsService(serviceType)
                : provider.GetService(serviceType) is not null;
    }
}
