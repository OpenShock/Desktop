using MudBlazor;

namespace OpenShock.Desktop.Ui.Utils;

public static class ThemeDefinition
{
    public static readonly MudTheme DesktopTheme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#e14a6d",
            PrimaryDarken = "#b31e40",
            Secondary = MudBlazor.Colors.Green.Accent4,
            AppbarBackground = MudBlazor.Colors.Red.Default,
            Background = "#2f2f2f",
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