namespace OpenShock.Desktop.ModuleBase.Models;

public struct LocalControlArgs
{
    /// <summary>
    /// The control commands that were sent, after filtering out shockers that may not be controlled.
    /// </summary>
    public required IReadOnlyList<ShockerControl> Shocks { get; init; }
    public string? CustomName { get; init; }
}
