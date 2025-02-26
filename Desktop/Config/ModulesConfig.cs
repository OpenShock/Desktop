namespace OpenShock.Desktop.Config;

public sealed class ModulesConfig
{
    public IList<Uri> ModuleRepositories { get; set; } = new List<Uri>();
    
    public IDictionary<string, ModuleTask> ModuleTasks { get; set; } = new Dictionary<string, ModuleTask>();
    
    

}