using OpenShock.Desktop.Backend;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.Services;
using OpenShock.SDK.CSharp.Models;
using Control = OpenShock.SDK.CSharp.Hub.Models.Control;

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
    
    public Task Control(IEnumerable<Control> shocks, string? customName = null)
    {
        return _backendHubManager.Control(shocks, customName);
    }

    public void LiveControl(IEnumerable<Guid> shockers, byte intensity, ControlType type)
    {
        _liveControlManager.ControlShockers(shockers, intensity, type);
    }

    public void ControlAllShockers(byte intensity, ControlType type)
    {
        _liveControlManager.ControlAllShockers(intensity, type);
    }
}