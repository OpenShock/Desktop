using OpenShock.Desktop.ModuleBase.StableInterfaces;

namespace OpenShock.Desktop.Models.BaseImpl;

public sealed class OpenShockHub : OpenShockHubBase, IOpenShockHub
{
    public required IReadOnlyList<IOpenShockShocker> Shockers { get; init; }
    public IHubStatus Status => GetStatus();

    public required Func<HubStatus> GetStatus { get; init; }
}