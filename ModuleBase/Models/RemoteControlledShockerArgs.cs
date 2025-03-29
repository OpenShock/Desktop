namespace OpenShock.Desktop.ModuleBase.Models;

public struct RemoteControlledShockerArgs
{
    public required ControlLogSender Sender { get; init; }
    public required IReadOnlyList<ControlLog> Logs { get; init; }
}

// Pretty much copy-pasted from OpenShock SDK

public class ControlLog
{
    public required GenericIn Shocker { get; init; }
    public required ControlType Type { get; init; }
    public required byte Intensity { get; init; }
    public required uint Duration { get; init; }
    public required DateTime ExecutedAt { get; init; }
}

public class GenericIn
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}

public class GenericIni : GenericIn
{
    public required Uri Image { get; init; }
}

public class ControlLogSender : GenericIni
{
    public required string ConnectionId { get; init; }
    public required string? CustomName { get; init; }
    public required IReadOnlyDictionary<string, object> AdditionalItems { get; init; }
}