namespace OpenShock.Desktop.ModuleBase.StableInterfaces;

public interface IOpenShockHubBase
{
    public Guid Id { get; }
    public string Name { get; }
    public DateTime CreatedOn { get; }
}