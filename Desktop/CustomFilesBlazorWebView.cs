using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.FileProviders;

namespace OpenShock.Desktop;

public class CustomFilesBlazorWebView : BlazorWebView
{
    public override IFileProvider CreateFileProvider(string contentRootDir)
    {
        var moduleManager = Handler!.MauiContext!.Services.GetRequiredService<ModuleManager.ModuleManager>();

        List<IFileProvider> fileProviders = new();
        fileProviders.Add(base.CreateFileProvider(contentRootDir));
        fileProviders.AddRange(moduleManager.Modules.Select(x => x.Value.Assembly).Distinct()
            .Select(x => new EmbeddedFileProvider(x, string.Empty)));
        
        return new CompositeFileProvider(fileProviders);
    }
}