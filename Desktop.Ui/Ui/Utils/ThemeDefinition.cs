using MudBlazor;
using OpenShock.Desktop.Config;

namespace OpenShock.Desktop.Ui.Utils;

public sealed class ThemeDefinition
{
    private readonly ConfigManager _configManager;

    public ThemeDefinition(ConfigManager configManager)
    {
        _configManager = configManager;
    }

    public MudTheme GetCurrentTheme()
    {
        return _configManager.Config.App.Theme switch
        {
            Theme.Dark => _dark,
            Theme.Midnight => _midnight,
            _ => _dark
        };
    }
    
    private readonly MudTheme _midnight = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#e14a6d",
            PrimaryDarken = "#b31e40",
            Secondary = MudBlazor.Colors.Green.Accent4,
            AppbarBackground = MudBlazor.Colors.Red.Default,
            Background = "#020202",
            Surface = "#050505",
            Dark = "#272727",
            DarkLighten = "#292929",
            GrayDefault = "#333333"
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px",
            DrawerWidthRight = "300px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["'Poppins', Roboto, Helvetica, Arial, sans-serif"]
            },
        }
    };
    
    private readonly MudTheme _dark = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#e14a6d",
            PrimaryDarken = "#b31e40",
            Secondary = MudBlazor.Colors.Green.Accent4,
            AppbarBackground = MudBlazor.Colors.Red.Default,
            Background = "#212121",
            Surface = "#1f1f1f",
            Dark = "#272727",
            DarkLighten = "#292929",
            GrayDefault = "#333333"
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px",
            DrawerWidthRight = "300px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["'Poppins', Roboto, Helvetica, Arial, sans-serif"]
            },
        }
    };
}