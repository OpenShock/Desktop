using System.Diagnostics.CodeAnalysis;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.Services;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public sealed class OpenShockService : IOpenShockService
{
    public required IOpenShockControl Control { get; init; }
    public required IOpenShockData Data { get; init; }
    
    [SetsRequiredMembers]
    public OpenShockService(BackendHubManager backendHubManager, LiveControlManager liveControlManager, OpenShockApi openShockApi)
    {
        Control = new OpenShockControl(backendHubManager, liveControlManager);
        Data = new OpenShockData()
        {
            Hubs = openShockApi.Hubs
        };
    }
}