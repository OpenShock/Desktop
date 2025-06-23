namespace OpenShock.Desktop.ModuleBase.StableInterfaces;

public interface IOpenShockHubWithToken : IOpenShockHubBase
{
    public string? Token { get; }
}