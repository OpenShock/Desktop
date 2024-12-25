using Semver;

namespace OpenShock.Desktop.ModuleManager.Repository;

public sealed class Module
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Uri? SourceUrl { get; init; }
    
    public required IDictionary<SemVersion, Version> Versions { get; init; }
    
}