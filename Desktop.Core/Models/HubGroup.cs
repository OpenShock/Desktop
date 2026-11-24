using OpenShock.Desktop.ModuleBase.StableInterfaces;

namespace OpenShock.Desktop.Models;

/// <summary>
/// A set of hubs shown together under one heading.
/// </summary>
/// <param name="Name">
/// Heading for the group, or null when the hubs need no attribution - the user's own hubs are just
/// hubs, while a shared one is worth naming the person it came from.
/// </param>
public sealed record HubGroup(string? Name, IReadOnlyList<IOpenShockHub> Hubs);
