using System.Runtime.InteropServices;
using MudBlazor.Utilities;
using OpenShock.Desktop.Ui.Utils;

#if WINDOWS 
namespace OpenShock.Desktop.Ui;

public partial class MauiApp
{
    public MauiApp()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var themeDefinition = activationState!.Context.Services.GetRequiredService<ThemeDefinition>();
        var backgroundColor =
            Color.FromArgb(themeDefinition.GetCurrentTheme().PaletteDark.Background.ToString(MudColorOutputFormats.Hex));
        var window = new Window(new MainPage())
        {
            MinimumHeight = 600,
            MinimumWidth = 1000,
            Title = "OpenShock",
            TitleBar = new TitleBar
            {
                Icon = ImageSource.FromFile("wwwroot/images/Icon512.png"),
                Title = "OpenShock",
                Subtitle = $"{Constants.Version} | {RuntimeInformation.RuntimeIdentifier}",
                BackgroundColor = backgroundColor
            }
        };
        
        return window;
    }
}
#endif