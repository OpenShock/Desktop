using OpenShock.Desktop.ModuleBase.Models;

namespace OpenShock.Desktop.ModuleBase.StableInterfaces;

public interface IOpenShockShocker
{
    public Guid Id { get; init; }
    public ushort RfId { get; init; }
    public ShockerModelType Model { get; init; }
    public string Name { get; init; }
    public bool IsPaused { get; init; }
    public DateTime CreatedOn { get; init; }
}