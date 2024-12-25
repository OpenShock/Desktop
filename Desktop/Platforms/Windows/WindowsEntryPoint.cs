#if WINDOWS
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using CommandLine;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using OpenShock.Desktop.Cli;
using OpenShock.Desktop.Cli.Uri;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Services.Pipes;
using OpenShock.Desktop.Utils;
using OpenShock.Desktop;
using OpenShock.Desktop.Platforms.Windows;
using WinRT;
using Application = Microsoft.UI.Xaml.Application;
using UriParser = OpenShock.Desktop.Cli.Uri.UriParser;

// ReSharper disable once CheckNamespace
namespace OpenShock.Desktop.Platforms.Windows;

public static class WindowsEntryPoint
{
    private const int ATTACH_PARENT_PROCESS = -1;

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int pid);

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            ParseHelper.Parse<MauiCliOptions>(args, Start);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void Start(MauiCliOptions config)
    {
        if (config.Console)
        {
            // Command line given, display console
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
                AllocConsole();
        }

        const string pipeName = @"\\.\pipe\OpenShock.Desktop";

        // TODO: Refactor this
        if (PipeHelper.EnumeratePipes().Any(x => x.Equals(pipeName, StringComparison.InvariantCultureIgnoreCase)))
        {
            using var pipeClientStream = new NamedPipeClientStream(".", "OpenShock.Desktop", PipeDirection.Out);
            pipeClientStream.Connect(500);

            using var writer = new StreamWriter(pipeClientStream);
            writer.AutoFlush = true;

            if (!string.IsNullOrEmpty(config.Uri))
            {
                var parsedUri = UriParser.Parse(config.Uri);
                var pipeMessage = parsedUri.Type switch
                {
                    UriParameterType.Show => new PipeMessage { Type = PipeMessageType.Show },
                    UriParameterType.Token => new PipeMessage
                    {
                        Type = PipeMessageType.Token, Data = string.Join('/', parsedUri.Arguments)
                    },
                    _ => null
                };

                if (pipeMessage != null) writer.WriteLine(JsonSerializer.Serialize(pipeMessage));

                return;
            }

            // Send show message
            writer.WriteLine(JsonSerializer.Serialize(new PipeMessage { Type = PipeMessageType.Show }));

            Console.WriteLine("Another instance of OpenShock Desktop is already running.");
            Environment.Exit(1);
            return;
        }

        if (config.Headless)
        {
            Console.WriteLine("Running in headless mode.");

            var host = HeadlessProgram.SetupHeadlessHost();
            OsTask.Run(host.Services.GetRequiredService<AuthService>().Authenticate);
            host.Run();

            return;
        }

        XamlCheckProcessRequirements();
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            // ReSharper disable once ObjectCreationAsStatement
            new App();
        });
    }
}
#endif