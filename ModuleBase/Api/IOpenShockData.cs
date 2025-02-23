using System.Collections.Immutable;
using OpenShock.Desktop.ModuleBase.Utils;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.Desktop.ModuleBase.Api;

public interface IOpenShockData
{
    public IObservableVariable<ImmutableArray<ResponseDeviceWithShockers>> Hubs { get; }
}