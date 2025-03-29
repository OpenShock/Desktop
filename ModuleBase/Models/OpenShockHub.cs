using System.Collections.Immutable;

namespace OpenShock.Desktop.ModuleBase.Models;

public class OpenShockHub : OpenShockHubBase
{
    public required ImmutableArray<OpenShockShocker> Shockers { get; set; }
}