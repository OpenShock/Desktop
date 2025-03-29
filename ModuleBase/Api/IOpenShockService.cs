namespace OpenShock.Desktop.ModuleBase.Api;

public interface IOpenShockService
{
    public IOpenShockControl Control { get; }
    public IOpenShockData Data { get; }
    
    public IOpenShockApi Api { get; }
}