using OpenShock.Desktop.ModuleBase.StableInterfaces;

namespace OpenShock.Desktop.Models.BaseImpl;

public class OpenShockHubBase : IOpenShockHubBase
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required DateTime CreatedOn { get; init; }
}