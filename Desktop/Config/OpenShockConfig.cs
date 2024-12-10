namespace OpenShock.Desktop.Config;

public sealed class OpenShockConfig
{
    public OpenShockConf OpenShock { get; set; } = new();
    public AppConfig App { get; set; } = new();
}