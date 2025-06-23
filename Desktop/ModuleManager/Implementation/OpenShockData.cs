using System.Collections.Immutable;
using OpenShock.Desktop.Models.BaseImpl;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.ModuleBase.StableInterfaces;
using OpenShock.Desktop.ModuleBase.Utils;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public class OpenShockData : IOpenShockData
{
    public required IObservableVariable<IReadOnlyList<IOpenShockHub>> Hubs { get; init; }
}