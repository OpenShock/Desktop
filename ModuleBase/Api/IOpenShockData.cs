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
    ///
    /// These carry less detail than <see cref="Hubs"/>: the shared shockers endpoint does not expose a shocker's
    /// RF id, model or creation date, so those are left at their default values and must not be relied upon. Only a
    /// shocker the user has explicitly enabled can be controlled, and then only within the permissions its owner
    /// granted - a control call targeting anything else is dropped.
    /// </summary>
    public IObservableVariable<IReadOnlyList<IOpenShockHub>> SharedHubs { get; }
}