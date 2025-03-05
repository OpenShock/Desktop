using Semver;

namespace OpenShock.Desktop.Utils;

public static class VersionUtils
{
    public static SemVersion? GetLatestReleaseVersion(this IEnumerable<SemVersion> versions, bool includePrerelease = false)
    {
        return versions.Where(x => x.IsRelease).OrderByDescending(x => x, SemVersion.PrecedenceComparer).FirstOrDefault();
    }
}