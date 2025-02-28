using System.Collections.Immutable;

namespace OpenShock.Desktop.ModuleBase.Models;

public class OpenShockHub
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required DateTime CreatedOn { get; set; }
    public required ImmutableArray<OpenShockShocker> Shockers { get; set; }
}