namespace OpenShock.Desktop.ModuleManager.Repository;

public sealed class Version
{
    public required Uri Url { get; init; }
    public required byte[] Sha256Hash { get; init; }
    public required Uri? ChangelogUrl { get; init; }
    public required Uri? ReleaseUrl { get; init; }
}