using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.SDK.CSharp.Models;
using ControlType = OpenShock.SDK.CSharp.Models.ControlType;

namespace OpenShock.Desktop.Utils;

public static class SdkDtoMappings
{
    public static Control ToSdkControl(this ShockerControl control)
    {
        return new Control
        {
            Id = control.Id,
            Type = (ControlType)control.Type,
            Intensity = control.Intensity,
            Duration = control.Duration,
            Exclusive = control.Exclusive
        };
    }
}