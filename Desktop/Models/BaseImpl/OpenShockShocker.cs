using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.ModuleBase.StableInterfaces;

namespace OpenShock.Desktop.Models.BaseImpl;

public sealed class OpenShockShocker : IOpenShockShocker
{
    public required Guid Id { get; init; }
    public required ushort RfId { get; init; }
    public required ShockerModelType Model { get; init; }
    public required string Name { get; init; }
    public required bool IsPaused { get; init; }
    public required DateTime CreatedOn { get; init; }
}