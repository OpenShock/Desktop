using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    /// a path is looked up among the module's embedded resources, as the Blazor UI serves them, or loaded as an
    /// Avalonia asset when it is an <c>avares://</c> URI (for modules with native pages).
    /// </summary>
    public static ModuleIcon Load(IconOneOf? icon, ModuleFileProvider files, ILogger logger)
    {
        if (icon is null) return None;

        try
        {
            return icon.Match(
                path => path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
                    ? LoadAsset(new Uri(path))
                    : LoadResource(path, files),
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
        return FromStream(stream, path);
    }

    private static ModuleIcon LoadAsset(Uri uri)
    {
        if (!AssetLoader.Exists(uri)) return None;

        using var stream = AssetLoader.Open(uri);
        return FromStream(stream, uri.AbsolutePath);
    }

    private static ModuleIcon FromStream(Stream stream, string path) =>
        path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            ? new ModuleIcon(null, new SvgImage { Source = SvgSource.LoadFromStream(stream) })
            : new ModuleIcon(null, new Bitmap(stream));
}
