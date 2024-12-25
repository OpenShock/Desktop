namespace OpenShock.Desktop.Config;

public sealed class ModulesConfig
{
    public IList<Uri> ModuleRepositories { get; set; } = new List<Uri>();
}