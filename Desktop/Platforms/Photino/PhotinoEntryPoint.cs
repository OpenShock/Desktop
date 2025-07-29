#if PHOTINO
using Microsoft.Extensions.FileProviders;
using OpenShock.Desktop.Cli;
using OpenShock.Desktop.Ui;
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
    
    private static void Start(CliOptions config)
    {
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

        app.MainWindow
            .SetIconFile("wwwroot/images/Icon512.png")
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