using Microsoft.AspNetCore.Components;
using MudBlazor.Services;
using OpenShock.Desktop.Ui.Utils;

namespace OpenShock.Desktop;

public static class UiBootstrap
{
    /// <summary>
    /// Registers what the Blazor UI needs on top of the core services from
    /// <see cref="Bootstrap.AddOpenShockDesktopServices"/>.
    /// </summary>
    public static void AddCommonBlazorServices(this IServiceCollection services)
    {
        services.AddMudServices();

        services.AddSingleton<ThemeDefinition>();

        // How the native shell and the Blazor UI talk to each other.
        services.AddSingleton<OpenShock.Desktop.Ui.WebUiBridge>();

        // Module components get their [ModuleInject] services, and a placeholder instead of an exception when
        // they fail to construct.
        services.AddScoped<IComponentActivator, OpenShockModuleComponentActivator>();
    }
}
