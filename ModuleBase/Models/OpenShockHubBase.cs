namespace OpenShock.Desktop.ModuleBase.Models;

public class OpenShockHubBase
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required DateTime CreatedOn { get; set; }
}