using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.ModuleBase.Utils;

namespace OpenShock.Desktop.ModuleBase.Api;

public interface IOpenShockAuth
{
    public Uri BackendBaseUri { get; }
    public IObservableVariable<AuthStateType> AuthState { get; }
}