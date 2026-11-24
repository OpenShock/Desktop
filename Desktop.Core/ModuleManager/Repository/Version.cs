namespace OpenShock.Desktop.ModuleManager.Repository;

public sealed class Version
{
    public required Uri Url { get; init; }
    public required byte[] Sha256Hash { get; init; }
    public Uri? ChangelogUrl { get; init; } = null;
    public Uri? ReleaseUrl { get; init; } = null;
}