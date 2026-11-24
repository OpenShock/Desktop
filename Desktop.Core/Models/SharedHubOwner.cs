using OpenShock.Desktop.ModuleBase.StableInterfaces;

namespace OpenShock.Desktop.Models;

/// <summary>
/// A user who has shared one or more of their hubs' shockers with the current user, grouped for display so the UI can
/// attribute shared hubs to the person that owns them.
/// </summary>
public sealed class SharedHubOwner
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public Uri? Image { get; init; }
    public required IReadOnlyList<IOpenShockHub> Hubs { get; init; }
}