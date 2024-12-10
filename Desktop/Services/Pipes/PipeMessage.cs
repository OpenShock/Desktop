namespace OpenShock.Desktop.Services.Pipes;

public sealed class PipeMessage
{
    public required PipeMessageType Type { get; set; }
    public object? Data { get; set; }
}