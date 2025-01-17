#if WEB
using OpenShock.Desktop.Cli;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Utils;

namespace OpenShock.Desktop.Platforms.Web;

public static class WebEntryPoint
{
    public static Task Main(string[] args)
    {
        return ParseHelper.ParseAsync<CliOptions>(args, Start);
    }

    private static async Task Start(CliOptions config)
    {
        if (config.Headless)
        {
            Console.WriteLine("Running in headless mode.");

            var host = HeadlessProgram.SetupHeadlessHost();
            OsTask.Run(host.Services.GetRequiredService<AuthService>().Authenticate);
            await host.RunAsync();

            return;
        }
        
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddOpenShockDesktopServices();
        builder.Services.AddCommonBlazorServices();
        
#if WINDOWS
            builder.Services.AddWindowsServices();
#endif

        var app = builder.Build();
        
        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        
        app.Services.StartOpenShockDesktopServices(true);
        
        OsTask.Run(app.Services.GetRequiredService<AuthService>().Authenticate);
        
        await app.RunAsync();
    }
}

#endif