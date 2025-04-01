using System.Runtime.InteropServices;

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
                BackgroundColor = Color.FromArgb("212121")
            }
        };
        
        return window;
    }
}
#endif