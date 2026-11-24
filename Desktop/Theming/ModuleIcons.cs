using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using OpenShock.Desktop.ModuleBase;

namespace OpenShock.Desktop.Theming;

/// <summary>A module icon, either as a Material geometry or as an image the module ships.</summary>
public sealed record ModuleIcon(Geometry? Geometry, IImage? Image)
{
    public static ModuleIcon None { get; } = new(null, null);

    public static ModuleIcon FromGeometry(Geometry geometry) => new(geometry, null);

    /// <summary>
    /// Resolves an <see cref="IconOneOf"/>: an SVG fragment (usually a MudBlazor icon string) becomes a geometry,
    /// a path is looked up among the module's embedded resources, as the Blazor UI serves them.
    /// </summary>
    public static ModuleIcon Load(IconOneOf? icon, ModuleFileProvider files, ILogger logger)
    {
        if (icon is null) return None;

        try
        {
            return icon.Match(
                path => LoadResource(path, files),
                svg => FromGeometry(MaterialIcons.FromSvg(svg)));
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not load module icon");
            return None;
        }
    }

    private static ModuleIcon LoadResource(string path, ModuleFileProvider files)
    {
        var file = files.GetFileInfo(path);
        if (!file.Exists) return None;

        using var stream = file.CreateReadStream();
        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            var source = SvgSource.LoadFromStream(stream);
            return new ModuleIcon(null, new SvgImage { Source = source });
        }

        return new ModuleIcon(null, new Bitmap(stream));
    }
}
