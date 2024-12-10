#if WINDOWS
using OpenShock.Desktop.Services;

// ReSharper disable once CheckNamespace
namespace OpenShock.Desktop.Platforms.Windows;

public static class WindowsServices
{
    public static void AddWindowsServices(this IServiceCollection services)
    {
        services.AddSingleton<ITrayService, WindowsTrayService>();
    }
}
#endif