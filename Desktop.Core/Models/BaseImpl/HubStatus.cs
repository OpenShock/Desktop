using OpenShock.Desktop.ModuleBase.StableInterfaces;

namespace OpenShock.Desktop.Models.BaseImpl;

public sealed class HubStatus : IHubStatus
{
    public bool Online { get; set; } = false;
}