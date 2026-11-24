using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Avalonia.Media;
using MudIcons = MudBlazor.Icons.Material.Filled;

namespace OpenShock.Desktop.Theming;

/// <summary>
/// The Material icons the shell uses, taken from MudBlazor's own icon strings so they match the Blazor UI exactly.
/// </summary>
public static partial class MaterialIcons
{
    // Static initializers run top to bottom, and the icons below need this.
    private static readonly ConcurrentDictionary<string, Geometry> Cache = new();

    public static Geometry SpaceDashboard { get; } = FromSvg(MudIcons.SpaceDashboard);
    public static Geometry ElectricBolt { get; } = FromSvg(MudIcons.ElectricBolt);
    public static Geometry ViewModule { get; } = FromSvg(MudIcons.ViewModule);
    public static Geometry ExtensionOff { get; } = FromSvg(MudIcons.ExtensionOff);
    public static Geometry ExpandMore { get; } = FromSvg(MudIcons.ExpandMore);
    public static Geometry Warning { get; } = FromSvg(MudIcons.Warning);
    public static Geometry Router { get; } = FromSvg(MudIcons.Router);
    public static Geometry Group { get; } = FromSvg(MudIcons.Group);
    public static Geometry Person { get; } = FromSvg(MudIcons.Person);
    public static Geometry Circle { get; } = FromSvg(MudIcons.Circle);
    public static Geometry AccountCircle { get; } = FromSvg(MudIcons.AccountCircle);
    public static Geometry PersonOff { get; } = FromSvg(MudIcons.PersonOff);
    public static Geometry Autorenew { get; } = FromSvg(MudIcons.Autorenew);
    public static Geometry Refresh { get; } = FromSvg(MudIcons.Refresh);
    public static Geometry BugReport { get; } = FromSvg(MudIcons.BugReport);
    public static Geometry Logout { get; } = FromSvg(MudIcons.Logout);
    public static Geometry Login { get; } = FromSvg(MudIcons.Login);
    public static Geometry Settings { get; } = FromSvg(MudIcons.Settings);
    public static Geometry Article { get; } = FromSvg(MudIcons.Article);

    /// <summary>
    /// Turns a MudBlazor icon string (the inside of a 24x24 SVG, e.g. <c>&lt;path d="..."/&gt;</c>) into a geometry.
    /// Paths with <c>fill="none"</c> are bounding boxes and are skipped.
    /// </summary>
    public static Geometry FromSvg(string svg) => Cache.GetOrAdd(svg, static svg =>
    {
        var group = new GeometryGroup { FillRule = FillRule.NonZero };

        foreach (Match element in ShapeRegex().Matches(svg))
        {
            var tag = element.Value;
            if (tag.Contains("fill=\"none\"", StringComparison.Ordinal)) continue;

            var geometry = element.Groups["tag"].Value switch
            {
                "path" when Attribute(tag, "d") is { } d => Geometry.Parse(d),
                "circle" => CircleShape(tag),
                "rect" => RectShape(tag),
                _ => null
            };

            if (geometry is not null) group.Children.Add(geometry);
        }

        return group;
    });

    private static Geometry? CircleShape(string tag) =>
        Number(tag, "cx") is { } cx && Number(tag, "cy") is { } cy && Number(tag, "r") is { } r
            ? new EllipseGeometry(new Avalonia.Rect(cx - r, cy - r, r * 2, r * 2))
            : null;

    private static Geometry? RectShape(string tag) =>
        Number(tag, "width") is { } w && Number(tag, "height") is { } h
            ? new RectangleGeometry(new Avalonia.Rect(Number(tag, "x") ?? 0, Number(tag, "y") ?? 0, w, h))
            : null;

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, $@"\s{name}=""([^""]*)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static double? Number(string tag, string name) =>
        double.TryParse(Attribute(tag, name), System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    [GeneratedRegex(@"<(?<tag>path|circle|rect)\b[^>]*>")]
    private static partial Regex ShapeRegex();
}
