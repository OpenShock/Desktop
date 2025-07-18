using OpenShock.Desktop.ModuleBase.Models;

namespace OpenShock.Desktop.ModuleBase;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class RequiredPermissionsAttribute : Attribute
{
    public IReadOnlyList<TokenPermissions> Permissions { get; init; }

    public RequiredPermissionsAttribute(params IReadOnlyList<TokenPermissions> permissions)
    {
        Permissions = permissions;
    }
}