using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using OpenShock.Desktop.Cli;
using OpenShock.Desktop.Cli.Uri;
using OpenShock.Desktop.Utils;
using Serilog;

namespace OpenShock.Desktop.Platforms.AvaloniaShell;

public static class AvaloniaEntryPoint
{
    /// <summary>
    /// How long to keep trying to hand our request to the running instance. It may still be
    /// starting up, in which case its pipe server is not listening yet.
    /// </summary>
    private static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(15);

    /// <summary>How long shutdown may take before we give up waiting for the host and exit anyway.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int pid);

    [STAThread]
    public static int Main(string[] args)
    {
        var exitCode = 0;
        ParseHelper.Parse<DesktopCliOptions>(args, options => exitCode = Start(options, args));
        return exitCode;
    }

    private static int Start(DesktopCliOptions options, string[] args)
    {
        // WinExe has no console of its own.
        if (options.Console && OperatingSystem.IsWindows() && !AttachConsole(AttachParentProcess)) AllocConsole();

        // Claim single instance ownership before doing anything else, headless included. Deciding this by
        // looking for the named pipe is not enough: the pipe only appears once the host has booted, and a
        // launch in that window would start a second instance that races the first over the module folder.
        if (!SingleInstanceGuard.TryAcquire())
        {
            if (SingleInstanceGuard.TryForwardToRunningInstance(options.Uri, ForwardTimeout))
            {
                Console.WriteLine("Another instance of OpenShock Desktop is already running. Forwarded request to it.");
                return 0;
            }

            Console.WriteLine("Another instance of OpenShock Desktop is already running, but it could not be reached.");
            return 1;
        }

        // We are the instance that stays up, so nobody is going to forward this to us over the
        // pipe. Hand it to the host instead once it has booted.
        StartupUri.Capture(options.Uri);

        if (options.Headless)
        {
            Console.WriteLine("Running in headless mode.");
            HeadlessProgram.SetupHeadlessHost().Run();
            return 0;
        }

        var isDebug = false;
#if DEBUG
        isDebug = true;
#endif

        var host = new BlazorServerHost(isDebug);

        int exitCode;
        try
        {
            exitCode = AppBuilder.Configure(() => new ShellApplication(host))
                .UsePlatformDetect()
                .WithInterFont()
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }
        finally
        {
            // Stop the host and dispose the services (hub connections, modules) without letting a stuck
            // one keep the process alive.
            var shutdown = Task.Run(async () =>
            {
                await host.App.StopAsync();
                await host.DisposeAsync();
            });
            if (!shutdown.Wait(ShutdownTimeout)) Console.WriteLine("Shutdown timed out, exiting anyway.");
            Log.CloseAndFlush();
        }

        return exitCode;
    }
}

public sealed class ShellApplication : Application
{
    private readonly BlazorServerHost _host;

    public ShellApplication(BlazorServerHost host)
    {
        _host = host;
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime) return;

        try
        {
            await Shell.StartAsync(lifetime, _host);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Failed to start the Avalonia shell");
            lifetime.Shutdown(1);
        }
    }
}
