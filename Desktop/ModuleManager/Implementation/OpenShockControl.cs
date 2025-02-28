using OpenShock.Desktop.Backend;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Utils;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public class OpenShockControl : IOpenShockControl
{
    private readonly BackendHubManager _backendHubManager;
    private readonly LiveControlManager _liveControlManager;

    public OpenShockControl(BackendHubManager backendHubManager, LiveControlManager liveControlManager)
    {
        _backendHubManager = backendHubManager;
        _liveControlManager = liveControlManager;
    }
    
    public Task Control(IEnumerable<ShockerControl> shocks, string? customName = null)
    {
        return _backendHubManager.Control(shocks.Select(SdkDtoMappings.ToSdkControl), customName);
    }

    public void LiveControl(IEnumerable<Guid> shockers, byte intensity, ControlType type)
    {
        _liveControlManager.ControlShockers(shockers, intensity, (SDK.CSharp.Models.ControlType)type);
    }

    public void ControlAllShockers(byte intensity, ControlType type)
    {
        _liveControlManager.ControlAllShockers(intensity, (SDK.CSharp.Models.ControlType)type);
    }
}