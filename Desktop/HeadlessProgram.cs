namespace OpenShock.Desktop;

public static class HeadlessProgram
{
    public static IHost SetupHeadlessHost()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddOpenShockDesktopServices();
        });
        
        var app = builder.Build();
        app.Services.StartOpenShockDesktopServices(true);
        
        return app;
    }
}