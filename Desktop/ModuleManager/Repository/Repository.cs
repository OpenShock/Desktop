using System.Collections.Immutable;

namespace OpenShock.Desktop.ModuleManager.Repository;

public sealed class Repository
{
    public required string Name { get; init; }
    public required string Id { get; init; }
    public required string Author { get; init; }
    public required Uri Url { get; init; }
    
    public required ImmutableDictionary<string, Module> Modules { get; init; }
}