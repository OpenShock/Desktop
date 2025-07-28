using Semver;

namespace OpenShock.Desktop.Config;

public sealed class AppConfig
{
    public bool CloseToTray { get; set; } = true;
    public bool MinimizeOnLaunch { get; set; } = false;
    public bool OSAutoStart { get; set; } = false;
    public bool VRCAutoStart { get; set; } = false;
    
    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Release;
    public SemVersion? LastIgnoredVersion { get; set; } = null;
    
    public Theme Theme { get; set; } = Theme.Dark;
}

public enum UpdateChannel
{
    Release,
    PreRelease
}

public enum Theme
{
    Dark,
    Midnight
}