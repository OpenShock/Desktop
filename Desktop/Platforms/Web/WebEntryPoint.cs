#if WEB
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.FileProviders;
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

        var moduleManager = app.Services.GetRequiredService<ModuleManager.ModuleManager>();

        // This modifies the WebRootFileProvider
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

        // actually create our file provider
        var compositeFileProvider =
            new CompositeFileProvider(app.Environment.WebRootFileProvider, new ModuleFileProvider(moduleManager));

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = compositeFileProvider
        });
        app.UseAntiforgery();


        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Services.StartOpenShockDesktopServices(true);

        await app.RunAsync();
    }
}

#endif