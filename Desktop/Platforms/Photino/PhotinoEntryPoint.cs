#if PHOTINO
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using OpenShock.Desktop.Cli;
using OpenShock.Desktop.Cli.Uri;
using OpenShock.Desktop.Services.Pipes;
using OpenShock.Desktop.Ui;
using Photino.Blazor;
using UriParser = OpenShock.Desktop.Cli.Uri.UriParser;

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

        // If another instance is already running, forward the request to it and exit.
        // .NET named pipes map to Unix domain sockets on Linux, so this is the same
        // single-instance mechanism the Windows entry point uses.
        if (TryForwardToRunningInstance(config.Uri)) return;


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

    /// <summary>
    /// Tries to connect to an already-running instance over the named pipe and forward the
    /// incoming URI (or a plain show request). Returns true if a running instance handled it,
    /// in which case this process should exit. Returns false if no instance is running, making
    /// this process the primary instance.
    /// </summary>
    private static bool TryForwardToRunningInstance(string? uri)
    {
        try
        {
            using var pipeClientStream = new NamedPipeClientStream(".", "OpenShock.Desktop", PipeDirection.Out);
            pipeClientStream.Connect(500);

            using var writer = new StreamWriter(pipeClientStream) { AutoFlush = true };

            PipeMessage? message = null;
            if (!string.IsNullOrEmpty(uri))
            {
                var parsedUri = UriParser.Parse(uri);
                message = parsedUri.Type switch
                {
                    UriParameterType.Show => new PipeMessage { Type = PipeMessageType.Show },
                    UriParameterType.Token => new PipeMessage
                    {
                        Type = PipeMessageType.Token, Data = string.Join('/', parsedUri.Arguments)
                    },
                    _ => null
                };
            }

            // Fall back to a show request (focus existing instance) for a bare relaunch.
            message ??= new PipeMessage { Type = PipeMessageType.Show };

            writer.WriteLine(JsonSerializer.Serialize(message));

            Console.WriteLine("Another instance of OpenShock Desktop is already running. Forwarded request to it.");
            return true;
        }
        catch (TimeoutException)
        {
            // No running instance — this process becomes the primary instance.
            return false;
        }
    }
}
#endif