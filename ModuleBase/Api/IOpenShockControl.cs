using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.MinimalEvents;

namespace OpenShock.Desktop.ModuleBase.Api;

public interface IOpenShockControl
{
    /// <summary>
    /// Send a control command to the backend. This is for "regular" commands and not frequent live control commands.
    /// </summary>
    /// <param name="shocks"></param>
    /// <param name="customName"></param>
    /// <returns></returns>
    public Task Control(IEnumerable<ShockerControl> shocks, string? customName = null);

    /// <summary>
    /// Intake a live control frame, and send it to the server whenever a tick happens.
    /// </summary>
    /// <param name="shockers"></param>
    /// <param name="intensity"></param>
    /// <param name="type"></param>
    public void LiveControl(IEnumerable<Guid> shockers, byte intensity, ControlType type);

    /// <summary>
    /// Control all enabled and online shockers
    /// </summary>
    /// <param name="intensity"></param>
    /// <param name="type"></param>
    public void ControlAllShockers(byte intensity, ControlType type);
    
    public IAsyncMinimalEventObservable<RemoteControlledShockerArgs> OnRemoteControlledShocker { get; }
}