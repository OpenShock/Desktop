using Semver;

namespace OpenShock.Desktop.Utils;

public static class VersionUtils
{
    public static SemVersion? GetLatestReleaseVersion(this IEnumerable<SemVersion> versions)
    {
        return versions.Where(x => x.IsRelease).OrderByDescending(x => x, SemVersion.PrecedenceComparer).FirstOrDefault();
    }
    
    public static SemVersion? GetLatestVersion(this IEnumerable<SemVersion> versions)
    {
        return versions.OrderByDescending(x => x, SemVersion.PrecedenceComparer).FirstOrDefault();
    }
}