using OpenShock.Desktop.ModuleBase.Models;

namespace OpenShock.Desktop.ModuleBase;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class RequiredPermissionAttribute : Attribute
{
    public TokenPermissions Permission { get; init; }

    public RequiredPermissionAttribute(TokenPermissions permission)
    {
        Permission = permission;
    }
}