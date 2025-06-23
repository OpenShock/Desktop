namespace OpenShock.Desktop.ModuleBase.StableInterfaces;

public interface IOpenShockHub : IOpenShockHubBase
{
    public IReadOnlyList<IOpenShockShocker> Shockers { get; init; }
    public IHubStatus Status { get; }
}