using OpenShock.Desktop.ModuleBase.StableInterfaces;

namespace OpenShock.Desktop.Models;

/// <summary>
/// A shocker together with the hub it lives on, so a shocker id can be resolved to both in one lookup instead of
/// scanning every hub.
/// </summary>
public sealed record ShockerLocation(IOpenShockHub Hub, IOpenShockShocker Shocker);
