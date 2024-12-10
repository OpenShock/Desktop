#if WINDOWS
using OpenShock.Desktop.Platforms.Windows;
#endif

namespace OpenShock.Desktop;

public static class HeadlessProgram
{
    public static IHost SetupHeadlessHost()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddOpenShockDesktopServices();

#if WINDOWS
            services.AddWindowsServices();
#endif
        });
        
        var app = builder.Build();
        app.Services.StartOpenShockDesktopServices(true);
        
        return app;
    }
}