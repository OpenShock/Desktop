using System.Collections.Immutable;
using System.Net.Mime;
using System.Reflection;
using OpenShock.Desktop.Ui.Utils;
using Semver;

namespace OpenShock.Desktop;

public static class Constants
{
    public static readonly string AppdataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenShock", "Desktop");
    public static readonly string LogsFolder = Path.Combine(AppdataFolder, "logs");
    public static readonly string ModuleData = Path.Combine(AppdataFolder, "moduleData");
    public static readonly string UserAgent = UserAgentUtil.GetUserAgent();
    
    public static readonly SemVersion Version = SemVersion.Parse(typeof(Constants).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, SemVersionStyles.Strict);
    public static readonly SemVersion VersionWithoutMetadata = Version.WithoutMetadata();

    public static readonly ImmutableArray<Uri> BuiltInModuleRepositories = [
        ..new List<Uri>
        {
            new("https://repo.openshock.org/1")
        }
    ];

    public static readonly ImmutableArray<string> ModuleZipAllowedMediaTypeNames =
    [
        ..new List<string>
        {
            MediaTypeNames.Application.Zip,
            MediaTypeNames.Application.Octet,
            "application/x-zip-compressed"
        }
    ];
}