using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Subjects;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Utils;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public class OpenShockData : IOpenShockData
{
    public required IObservableVariable<ImmutableArray<ResponseDeviceWithShockers>> Hubs { get; init; }
}