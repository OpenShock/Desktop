namespace OpenShock.Desktop.ModuleBase.Models;

public class OpenShockShocker
{
    public required Guid Id { get; set; }
    public required ushort RfId { get; set; }
    public required ShockerModelType Model { get; set; }
    public required string Name { get; set; }
    public required bool IsPaused { get; set; }
    public required DateTime CreatedOn { get; set; }
}