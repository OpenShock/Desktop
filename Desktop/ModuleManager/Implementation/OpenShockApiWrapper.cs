using OneOf;
using OneOf.Types;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Models;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public class OpenShockApiWrapper : IOpenShockApi
{
    private readonly OpenShockApi _openShockApi;

    public OpenShockApiWrapper(OpenShockApi openShockApi)
    {
        _openShockApi = openShockApi;
    }
    
    public async Task<OneOf<Success<OpenShockHubWithToken>, NotFound, UnauthenticatedError>> GetHub(Guid hubId, CancellationToken cancellationToken = default)
    {
        if (_openShockApi.Client == null) return new UnauthenticatedError();
        
        var result = await _openShockApi.Client.GetDevice(hubId, cancellationToken);

        return result.Match<OneOf<Success<OpenShockHubWithToken>, NotFound, UnauthenticatedError>>(success =>
            {
                var hub = success.Value;
                return new Success<OpenShockHubWithToken>(new OpenShockHubWithToken()
                {
                    Id = hub.Id,
                    Name = hub.Name,
                    Token = hub.Token,
                    CreatedOn = hub.CreatedOn,
                });
            },
            found => found,
            error => new UnauthenticatedError());
        
    }
}