#if MAUI
using Microsoft.AspNetCore.Components;
using Microsoft.Maui.LifecycleEvents;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Services.Pipes;
using MauiApp = OpenShock.Desktop.Ui.MauiApp;
#if WINDOWS
using OpenShock.Desktop.Platforms.Windows;
#endif

namespace OpenShock.Desktop;

public static class MauiProgram
{
    private static OpenShockConfig? _config;
    private static PipeServerService? _pipeServerService;

    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        try
        {
            return CreateMauiAppInternal();
        } catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    private static Microsoft.Maui.Hosting.MauiApp CreateMauiAppInternal()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();

        // <---- Services ---->

        builder.Services.AddOpenShockDesktopServices();
        builder.Services.AddCommonBlazorServices();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddScoped<IComponentActivator, OpenShockModuleComponentActivator>();

#if WINDOWS
        builder.Services.AddWindowsServices();

        builder.ConfigureLifecycleEvents(lifecycleBuilder =>
        {
            lifecycleBuilder.AddWindows(windowsLifecycleBuilder =>
            {
                windowsLifecycleBuilder.OnWindowCreated(window =>
                {
                    var appWindow = WindowUtils.GetAppWindow(window);

                    _pipeServerService?.OnMessageReceived.SubscribeAsync(_ =>
                    {
                        appWindow.ShowOnTop();

                        return Task.CompletedTask;
                    }).AsTask().Wait();

                    //When user execute the closing method, we can push a display alert. If user click Yes, close this application, if click the cancel, display alert will dismiss.
                    appWindow.Closing += async (s, e) =>
                    {
                        e.Cancel = true;

                        if (_config?.App.CloseToTray ?? false)
                        {
                            appWindow.Hide();
                            return;
                        }

                        if (Application.Current == null) return;

                        var page = Application.Current.Windows[0].Page;
                        
                        if(page == null) return;
                        
                        var result = await page.DisplayAlert(
                            "Close?",
                            "Do you want to close OpenShock?",
                            "Yes",
                            "Cancel");

                        if (result) Application.Current.Quit();
                    };
                });
            });
        });
#endif

        // <---- App ---->

        builder
            .UseMauiApp<MauiApp>();

        var app = builder.Build();

        _config = app.Services.GetRequiredService<ConfigManager>().Config;
        _pipeServerService = app.Services.GetRequiredService<PipeServerService>();
        
        app.Services.StartOpenShockDesktopServices(false);

        return app;
    }
}
#endif