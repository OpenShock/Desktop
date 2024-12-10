using System.Reflection;
using ModuleBase;

namespace OpenShock.Desktop.ModuleManager;

public sealed class LoadedModule
{
    public required Assembly Assembly { get; init; }
    public required IModule Module { get; init; }
}