using System.Diagnostics.CodeAnalysis;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.Services;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public sealed class OpenShockService : IOpenShockService, IAsyncDisposable
{
    public required IOpenShockControl Control { get; init; }
    public required IOpenShockData Data { get; init; }
    public required IOpenShockApi Api { get; init; }
    public IOpenShockAuth Auth { get; init; }

    private readonly OpenShockControl _controlInstance;
    
    [SetsRequiredMembers]
    public OpenShockService(IServiceProvider serviceProvider)
    {
        var backendHubManager = serviceProvider.GetRequiredService<BackendHubManager>();
        var liveControlManager = serviceProvider.GetRequiredService<LiveControlManager>();
        var openShockApi = serviceProvider.GetRequiredService<OpenShockApi>();
        var configManager = serviceProvider.GetRequiredService<ConfigManager>();
        var authService = serviceProvider.GetRequiredService<AuthService>();
        
        Control = _controlInstance = new OpenShockControl(backendHubManager, liveControlManager);
        Data = new OpenShockData
        {
            Hubs = openShockApi.Hubs
        };
        Api = new OpenShockApiWrapper(openShockApi);
        Auth = new OpenShockAuth
        {
            AuthState = authService.AuthState,
            BackendBaseUri = configManager.Config.OpenShock.Backend
        };
    }

    private bool _disposed;
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        await _controlInstance.DisposeAsync();
    }
}