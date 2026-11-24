using System.Collections.Immutable;
using Semver;

namespace OpenShock.Desktop.ModuleManager.Repository;

public sealed class Module
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public Uri? SourceUrl { get; init; } = null;
    public Uri? IconUrl { get; init; } = null;
    
    public required ImmutableDictionary<SemVersion, Version> Versions { get; init; }
    
}