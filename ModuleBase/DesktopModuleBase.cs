using OpenShock.Desktop.ModuleBase.Navigation;

namespace OpenShock.Desktop.ModuleBase;

public abstract class DesktopModuleBase
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public virtual string? IconPath { get; } = null;

    public virtual IReadOnlyCollection<NavigationItem> NavigationComponents { get; } = Array.Empty<NavigationItem>();

    public virtual void Start()
    {
    }
}