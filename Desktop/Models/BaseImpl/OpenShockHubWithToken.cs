using OpenShock.Desktop.ModuleBase.StableInterfaces;

namespace OpenShock.Desktop.Models.BaseImpl;

public sealed class OpenShockHubWithToken : OpenShockHubBase, IOpenShockHubWithToken 
{
    public string? Token { get; init; }
}