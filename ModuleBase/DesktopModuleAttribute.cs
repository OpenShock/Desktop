using System.Diagnostics.CodeAnalysis;
using OpenShock.Desktop.ModuleBase.Models;

namespace OpenShock.Desktop.ModuleBase;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public class DesktopModuleAttribute : Attribute
{
    [SetsRequiredMembers]
    public DesktopModuleAttribute(Type moduleType, string id, string name)
    {
        Id = id.ToLowerInvariant();
        Name = name;
        ModuleType = moduleType;
    }
    
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Type ModuleType { get; init; }
}