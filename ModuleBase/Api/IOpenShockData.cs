using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.ModuleBase.StableInterfaces;
using OpenShock.Desktop.ModuleBase.Utils;

namespace OpenShock.Desktop.ModuleBase.Api;

public interface IOpenShockData
{
    public IObservableVariable<IReadOnlyList<IOpenShockHub>> Hubs { get; }

    /// <summary>
    /// Hubs owned by other users that have shared one or more shockers with the current user.
    /// </summary>
    public IObservableVariable<IReadOnlyList<IOpenShockHub>> SharedHubs { get; }
}