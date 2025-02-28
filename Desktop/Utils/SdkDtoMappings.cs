﻿using System.Collections.Immutable;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.SDK.CSharp.Models;
using ControlType = OpenShock.SDK.CSharp.Models.ControlType;
using ShockerModelType = OpenShock.Desktop.ModuleBase.Models.ShockerModelType;

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
    
    public static OpenShockShocker ToSdkShocker(this ShockerResponse shocker)
    {
        return new OpenShockShocker
        {
            Id = shocker.Id,
            Name = shocker.Name,
            CreatedOn = shocker.CreatedOn,
            IsPaused = shocker.IsPaused,
            Model = (ShockerModelType)shocker.Model,
            RfId = shocker.RfId
        };
    }
    
    public static OpenShockHub ToSdkHub(this ResponseDeviceWithShockers hub)
    {
        return new OpenShockHub
        {
            Id = hub.Id,
            Name = hub.Name,
            CreatedOn = hub.CreatedOn,
            Shockers = [..hub.Shockers.Select(ToSdkShocker)]
        };
    }
}