﻿using MudBlazor.Services;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Logging;
using OpenShock.Desktop.ModuleManager.Repository;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Services.Pipes;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Hub;
using Serilog;

namespace OpenShock.Desktop;

public static class Bootstrap
{
    public static void AddOpenShockDesktopServices(this IServiceCollection services)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Filter.ByExcluding(ev =>
                ev.Exception is InvalidDataException a && a.Message.StartsWith("Invocation provides")).Filter
            .ByExcluding(x => x.MessageTemplate.Text.StartsWith("Failed to find handler for"))
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
            .WriteTo.UiLogSink()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

        // ReSharper disable once RedundantAssignment
        var isDebug = Environment.GetCommandLineArgs()
            .Any(x => x.Equals("--debug", StringComparison.InvariantCultureIgnoreCase));

#if DEBUG_WINDOWS || DEBUG_PHOTINO || DEBUG_WEB
        isDebug = true;
#endif
        if (isDebug)
        {
            Console.WriteLine("Debug mode enabled");
            loggerConfiguration.MinimumLevel.Verbose();
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        services.AddSerilog(Log.Logger);

        services.AddMemoryCache();

        services.AddSingleton<PipeServerService>();

        services.AddSingleton<ConfigManager>();

        services.AddSingleton<Updater>();

        services.AddSingleton<OpenShockApi>();
        services.AddSingleton<OpenShockHubClient>();
        services.AddSingleton<BackendHubManager>();

        services.AddSingleton<LiveControlManager>();

        services.AddSingleton<AuthService>();
        
        services.AddSingleton<StatusHandler>();

        services.AddSingleton<RepositoryManager>();
        services.AddSingleton<ModuleManager.ModuleManager>();
        // foreach (var plugin in ModuleManager.ModuleManager.Plugins)
        // {
        //     plugin.RegisterServices(services);
        // }

        services.AddSingleton<StartupService>();
    }

    public static void AddCommonBlazorServices(this IServiceCollection services)
    {
#if DEBUG_WINDOWS || DEBUG_PHOTINO
        services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddMudServices();
    }

    public static void StartOpenShockDesktopServices(this IServiceProvider services, bool headless)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
        try
        {
            StartOpenShockDesktopServicesInternal(services, headless);
        } catch (Exception e)
        {
            logger.LogError(e, "Failed to start OpenShock Desktop services");
            throw;
        }
    }

    private static void StartOpenShockDesktopServicesInternal(this IServiceProvider services, bool headless)
    {
        #region SystemTray

#if WINDOWS
        if (headless)
        {
            var applicationThread = new Thread(() =>
            {
                services.GetService<ITrayService>()?.Initialize();
                System.Windows.Forms.Application.Run();
            });
            applicationThread.Start();
        }
        else services.GetService<ITrayService>()?.Initialize();
#else
        services.GetService<ITrayService>()?.Initialize();
#endif

        #endregion

        var config = services.GetRequiredService<ConfigManager>();


        // <---- Warmup ---->
        services.GetRequiredService<PipeServerService>().StartServer();

        var startupService = services.GetRequiredService<StartupService>();
        var startupTask = OsTask.Run(startupService.StartupApp);
    }
}