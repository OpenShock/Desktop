#if WINDOWS
using System.Runtime.InteropServices;
using CommandLine;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using OpenShock.Desktop.Cli;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Utils;
using OpenShock.Desktop;
using OpenShock.Desktop.Platforms.Windows;
using WinRT;
using Application = Microsoft.UI.Xaml.Application;

// ReSharper disable once CheckNamespace
namespace OpenShock.Desktop.Platforms.Windows;

public static class WindowsEntryPoint
{
    // ReSharper disable once InconsistentNaming
    private const int ATTACH_PARENT_PROCESS = -1;

    /// <summary>
    /// How long to keep trying to hand our request to the running instance. It may still be
    /// starting up, in which case its pipe server is not listening yet.
    /// </summary>
    private static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(15);

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

        // Claim single instance ownership before doing anything else. This used to be decided by
        // looking for the named pipe, but that pipe only appears once the host has booted far
        // enough to start PipeServerService - a minute after launch on a cold start. A launch
        // inside that window saw no pipe and started a second full instance, and the two then
        // raced over the module folder: one deletes module files the other has loaded.
        if (!SingleInstanceGuard.TryAcquire())
        {
            if (SingleInstanceGuard.TryForwardToRunningInstance(config.Uri, ForwardTimeout))
            {
                Console.WriteLine("Another instance of OpenShock Desktop is already running. Forwarded request to it.");
                return;
            }

            Console.WriteLine("Another instance of OpenShock Desktop is already running, but it could not be reached.");
            Environment.Exit(1);
            return;
        }

        if (config.Headless)
        {
            Console.WriteLine("Running in headless mode.");

            var host = HeadlessProgram.SetupHeadlessHost();
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