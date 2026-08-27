#if PHOTINO
using Microsoft.Extensions.FileProviders;
using OpenShock.Desktop.Cli;
using OpenShock.Desktop.Ui;
using OpenShock.Desktop.Utils;
using Photino.Blazor;

namespace OpenShock.Desktop.Platforms.Photino;

public static class PhotinoEntryPoint
{
    private static readonly ModuleFileProvider ModuleFileProvider = new ModuleFileProvider();
    
    // [STAThread]
    // static void Main(string[] args)
    // {
    //     var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);
    //     appBuilder.Services
    //         .AddLogging();
    //
    //     // register root component
    //     appBuilder.RootComponents.Add<Main>("#app");
    //
    //     var app = appBuilder.Build();
    //
    //     // customize window
    //     app.MainWindow
    //         .SetIconFile("wwwroot/images/Icon512.png")
    //         .SetTitle("OpenShock Desktop");
    //
    //     AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
    //     {
    //         app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
    //     };
    //
    //     app.Run();
    // }
    
    
    [STAThread]
    public static void Main(string[] args)
    {
        ParseHelper.Parse<CliOptions>(args, Start);
    }
    
    /// <summary>
    /// How long to keep trying to hand our request to the running instance. It may still be
    /// starting up, in which case its pipe server is not listening yet.
    /// </summary>
    private static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(15);

    private static void Start(CliOptions config)
    {
        // Claim single instance ownership before doing anything else, headless included. This
        // used to be decided by trying to connect to the named pipe, but that pipe only appears
        // once the host has booted far enough to start PipeServerService - a minute after launch
        // on a cold start. A launch inside that window found no pipe and started a second full
        // instance, and the two then raced over the module folder: one deletes module files the
        // other has loaded.
        if (!SingleInstanceGuard.TryAcquire())
        {
            Console.WriteLine(SingleInstanceGuard.TryForwardToRunningInstance(config.Uri, ForwardTimeout)
                ? "Another instance of OpenShock Desktop is already running. Forwarded request to it."
                : "Another instance of OpenShock Desktop is already running, but it could not be reached.");
            return;
        }

        if (config.Headless)
        {
            Console.WriteLine("Running in headless mode.");

            var host = HeadlessProgram.SetupHeadlessHost();
            host.Run();

            return;
        }


        var compositeFileProvider = new CompositeFileProvider(
            new PhysicalFileProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot")),
            ModuleFileProvider);
        
        var builder = PhotinoBlazorAppBuilder.CreateDefault(compositeFileProvider);

        builder.Services.AddOpenShockDesktopServices();
        builder.Services.AddCommonBlazorServices();

        
        builder.Services.Configure((Action<PhotinoBlazorAppConfiguration>) (opts =>
        {
            opts.HostPage = "photino.html";
        }));
        
        builder.RootComponents.Add<Main>("#app");

        var app = builder.Build();

        // Resolve the icon against the app's base directory; the working directory is
        // wherever the AppImage was launched from (e.g. ~/Downloads), not the install dir.
        var iconFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "Icon512.png");

        app.MainWindow
            .SetIconFile(iconFile)
            .SetTitle("OpenShock Desktop");
        
        app.MainWindow.MinHeight = 600;
        app.MainWindow.MinWidth = 1000;
        
        app.Services.StartOpenShockDesktopServices(true);
        
        ModuleFileProvider.SetModuleManager(app.Services.GetRequiredService<ModuleManager.ModuleManager>());
        
        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
        };
        
        app.Run();
    }

}
#endif