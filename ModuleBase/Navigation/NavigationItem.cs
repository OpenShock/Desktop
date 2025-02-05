using OneOf;

namespace OpenShock.Desktop.ModuleBase.Navigation;

public sealed class NavigationItem
{
    public required string Name { get; set; }
    public required Type ComponentType { get; set; }
    
    public IconOneOf? Icon { get; set; } = null;
}