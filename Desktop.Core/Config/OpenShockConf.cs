namespace OpenShock.Desktop.Config;

public sealed class OpenShockConf
{
    public Uri Backend { get; set; } = new("https://api.openshock.app");
    public string Token { get; set; } = "";
    /// <summary>
    /// Per-shocker overrides. Sparse: an entry exists only where the user has made an explicit choice, and anything
    /// absent is disabled. Read through <see cref="Backend.OpenShockApi.IsShockerEnabled"/>, not directly.
    /// </summary>
    public IReadOnlyDictionary<Guid, ShockerConf> Shockers { get; set; } = new Dictionary<Guid, ShockerConf>();

    public sealed class ShockerConf
    {
        public bool Enabled { get; set; }
    }
}