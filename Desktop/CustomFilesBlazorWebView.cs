#if MAUI
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.FileProviders;

namespace OpenShock.Desktop;

/// <summary>
/// BlazorWebView that uses our custom ModuleFuleProvider.
/// </summary>
public partial class CustomFilesBlazorWebView : BlazorWebView
{
    public override IFileProvider CreateFileProvider(string contentRootDir)
    {
        var moduleManager = Handler!.MauiContext!.Services.GetRequiredService<ModuleManager.ModuleManager>();
        return new CompositeFileProvider(base.CreateFileProvider(contentRootDir), new ModuleFileProvider(moduleManager));
    }
}
#endif