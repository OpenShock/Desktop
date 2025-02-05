using OpenShock.Desktop.ModuleBase.Navigation;

namespace OpenShock.Desktop.ModuleBase;

public abstract class DesktopModuleBase
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public virtual string? IconPath { get; } = null;

    public abstract IReadOnlyCollection<NavigationItem> NavigationComponents { get; }

    public virtual void Start()
    {
    }
}