using OneOf.Types;
using OneOf;
using OpenShock.Desktop.ModuleBase.Models;

namespace OpenShock.Desktop.ModuleBase.Api;

public interface IOpenShockApi
{
    /// <summary>
    /// Get a device with its token if you have permissions
    /// </summary>
    /// <param name="hubId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<OneOf<Success<OpenShockHubWithToken>, NotFound, UnauthenticatedError>> GetHub(Guid hubId, CancellationToken cancellationToken = default);
}

public struct UnauthenticatedError;