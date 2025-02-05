using OneOf;

namespace OpenShock.Desktop.ModuleBase;

/// <summary>
/// string 1: Path to an icon file
/// string 2: Base64 encoded SVG icon part
/// </summary>
public class IconOneOf : OneOfBase<string, string>
{
    protected IconOneOf(OneOf<string, string> input) : base(input)
    {
    }
    
    public static IconOneOf FromPath(string path) => new IconOneOf(OneOf<string, string>.FromT0(path));
    public static IconOneOf FromSvg(string base64) => new IconOneOf(OneOf<string, string>.FromT1(base64));
}