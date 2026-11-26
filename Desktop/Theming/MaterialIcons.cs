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
    public static Geometry Info { get; } = FromSvg(MudIcons.Info);
    public static Geometry Key { get; } = FromSvg(MudIcons.Key);
    public static Geometry Extension { get; } = FromSvg(MudIcons.Extension);
    public static Geometry OpenInNew { get; } = FromSvg(MudIcons.OpenInNew);
    public static Geometry Add { get; } = FromSvg(MudIcons.Add);
    public static Geometry Download { get; } = FromSvg(MudIcons.Download);
    public static Geometry Close { get; } = FromSvg(MudIcons.Close);
    public static Geometry ErrorOutline { get; } = FromSvg(MudIcons.ErrorOutline);
    public static Geometry Update { get; } = FromSvg(MudIcons.Update);
    public static Geometry Bolt { get; } = FromSvg(MudIcons.Bolt);
    public static Geometry Send { get; } = FromSvg(MudIcons.Send);
    public static Geometry Pause { get; } = FromSvg(MudIcons.Pause);
    public static Geometry PlayArrow { get; } = FromSvg(MudIcons.PlayArrow);
    public static Geometry Vibration { get; } = FromSvg(MudIcons.Vibration);
    public static Geometry VolumeUp { get; } = FromSvg(MudIcons.VolumeUp);
    public static Geometry Sensors { get; } = FromSvg(MudIcons.Sensors);
    public static Geometry CheckCircle { get; } = FromSvg(MudIcons.CheckCircle);
    public static Geometry QuestionMark { get; } = FromSvg(MudIcons.QuestionMark);
    public static Geometry Apps { get; } = FromSvg(MudIcons.Apps);
    public static Geometry FolderOpen { get; } = FromSvg(MudIcons.FolderOpen);
    public static Geometry RestartAlt { get; } = FromSvg(MudIcons.RestartAlt);
    public static Geometry DesktopWindows { get; } = FromSvg(MudIcons.DesktopWindows);
    public static Geometry InstallDesktop { get; } = FromSvg(MudIcons.InstallDesktop);
    public static Geometry CodeOff { get; } = FromSvg(MudIcons.CodeOff);
    public static Geometry ArrowDropDown { get; } = FromSvg(MudIcons.ArrowDropDown);
    public static Geometry FormatListNumbered { get; } = FromSvg(MudIcons.FormatListNumbered);
    public static Geometry RemoveFromQueue { get; } = FromSvg(MudIcons.RemoveFromQueue);
    public static Geometry Inventory2 { get; } = FromSvg(MudIcons.Inventory2);
    public static Geometry Palette { get; } = FromSvg(MudIcons.Palette);
    public static Geometry Storage { get; } = FromSvg(MudIcons.Storage);
    public static Geometry Tune { get; } = FromSvg(MudIcons.Tune);
    public static Geometry Adjust { get; } = FromSvg(MudIcons.Adjust);
    public static Geometry RemoveCircleOutline { get; } = FromSvg(MudIcons.RemoveCircleOutline);
    public static Geometry KeyboardArrowDown { get; } = FromSvg(MudIcons.KeyboardArrowDown);
    public static Geometry KeyboardArrowUp { get; } = FromSvg(MudIcons.KeyboardArrowUp);
    public static Geometry Error { get; } = FromSvg(MudIcons.Error);
    public static Geometry ChevronRight { get; } = FromSvg(MudIcons.ChevronRight);
    public static Geometry Delete { get; } = FromSvg(MudIcons.Delete);
    public static Geometry CheckBox { get; } = FromSvg(MudIcons.CheckBox);
    public static Geometry CheckBoxOutlineBlank { get; } = FromSvg(MudIcons.CheckBoxOutlineBlank);
    public static Geometry GitHub { get; } = FromSvg(MudBlazor.Icons.Custom.Brands.GitHub);

    /// <summary>
    /// Turns a MudBlazor icon string (the inside of a 24x24 SVG, e.g. <c>&lt;path d="..."/&gt;</c>) into a geometry.
    /// Shapes with <c>fill="none"</c> are bounding boxes and are skipped.
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
                "polygon" when Attribute(tag, "points") is { } points => PolygonShape(points),
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

    private static Geometry? PolygonShape(string points)
    {
        var pairs = points.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return pairs.Length < 2 ? null : Geometry.Parse("M" + string.Join(" L", pairs) + " Z");
    }

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

    [GeneratedRegex(@"<(?<tag>path|circle|rect|polygon)\b[^>]*>")]
    private static partial Regex ShapeRegex();
}
