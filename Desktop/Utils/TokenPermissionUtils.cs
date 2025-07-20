using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.Desktop.Utils;

public static class TokenPermissionUtils
{
    public static PermissionType GetPermissionType(this TokenPermissions permission)
    {
        return permission switch
        {
            TokenPermissions.Shockers_Use => PermissionType.Shockers_Use,
            TokenPermissions.Shockers_Edit => PermissionType.Shockers_Edit,
            TokenPermissions.Shockers_Pause => PermissionType.Shockers_Pause,
            TokenPermissions.Devices_Edit => PermissionType.Devices_Edit,
            TokenPermissions.Devices_Auth => PermissionType.Devices_Auth,
            _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null)
        };
    }
}