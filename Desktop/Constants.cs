using System.Reflection;
using Semver;

namespace OpenShock.Desktop;

public static class Constants
{
    public static readonly string AppdataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"OpenShock\Desktop");
    
    public static readonly SemVersion Version = SemVersion.Parse(typeof(Constants).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, SemVersionStyles.Strict);
    public static readonly SemVersion VersionWithoutMetadata = Version.WithoutMetadata();

    public static readonly IReadOnlyList<Uri> BuiltInModuleRepositories = new List<Uri>
    {
        new("https://nas.luc.cat/openshockmodules.json")
    };
}