namespace ModuleBase;

public interface IModule
{
    public string Id { get; }
    public string Name { get; }
    public Type RootComponentType { get; }
    
    public string? IconPath { get; }
    
    public void Start()
    {
    }
}