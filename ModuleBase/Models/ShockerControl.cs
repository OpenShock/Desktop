using System.ComponentModel.DataAnnotations;

namespace OpenShock.Desktop.ModuleBase.Models;

public class ShockerControl
{
    public required Guid Id { get; set; }
    public required ControlType Type { get; set; }
    [Range(1, 100)]
    public required byte Intensity { get; set; }
    [Range(300, 30000)]
    public required ushort Duration { get; set; }
    public bool Exclusive { get; set; } = false;
}