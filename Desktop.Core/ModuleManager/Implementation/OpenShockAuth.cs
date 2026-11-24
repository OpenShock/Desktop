using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.ModuleBase.Utils;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public class OpenShockAuth : IOpenShockAuth
{
    public required Uri BackendBaseUri { get; init; }
    public required IObservableVariable<AuthStateType> AuthState { get; init; }
}