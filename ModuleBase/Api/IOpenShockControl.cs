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
    /// Send a control command to every shocker the user has enabled, owned and shared alike. This is the "all
    /// shockers" counterpart to <see cref="Control"/>; use it rather than assembling the list from
    /// <see cref="IOpenShockData.Hubs"/>, which leaves out every shared shocker.
    /// </summary>
    /// <param name="duration">How long the command runs for, in milliseconds.</param>
    /// <param name="intensity"></param>
    /// <param name="type"></param>
    /// <param name="exclusive"></param>
    /// <param name="customName"></param>
    public Task ControlAll(ushort duration, byte intensity, ControlType type, bool exclusive = false,
        string? customName = null);

    /// <summary>
    /// Intake a live control frame for all enabled and online shockers, owned and shared alike. This is the live
    /// control counterpart to <see cref="LiveControl"/>, not a regular control command - see
    /// <see cref="ControlAll"/> for that.
    /// </summary>
    /// <param name="intensity"></param>
    /// <param name="type"></param>
    public void ControlAllShockers(byte intensity, ControlType type);
    
    /// <summary>
    /// A shocker has been remotely controlled by another location.
    /// </summary>
    public IAsyncMinimalEventObservable<RemoteControlledShockerArgs> OnRemoteControlledShocker { get; }
    
    /// <summary>
    /// This OpenShock Desktop instance has controlled a shocker.
    /// </summary>
    public IAsyncMinimalEventObservable<RemoteControlledShockerArgs> OnLocalControlledShocker { get; }
}