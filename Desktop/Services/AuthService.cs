using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.ModuleBase.Utils;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.Desktop.Services;

public sealed class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly BackendHubManager _backendHubManager;
    private readonly OpenShockHubClient _hubClient;
    private readonly LiveControlManager _liveControlManager;
    private readonly OpenShockApi _apiClient;
    private readonly ConfigManager _configManager;
    public SelfResponse? SelfResponse { get; private set; } 

    // NotAuthed
    // FailedAuth
    // Authed
    

    public IObservableVariable<AuthStateType> AuthState => _authState;
    private readonly ObservableVariable<AuthStateType> _authState = new(AuthStateType.NotAuthed);

    public AuthService(ILogger<AuthService> logger,
        BackendHubManager backendHubManager,
        OpenShockHubClient hubClient,
        LiveControlManager liveControlManager,
        OpenShockApi apiClient,
        ConfigManager configManager)
    {
        _logger = logger;
        _backendHubManager = backendHubManager;
        _hubClient = hubClient;
        _liveControlManager = liveControlManager;
        _apiClient = apiClient;
        _configManager = configManager;
    }

    private readonly SemaphoreSlim _authLock = new(1, 1);
    
    public async Task Authenticate()
    {
        await _authLock.WaitAsync();
        
        if (_authState.Value == AuthStateType.Authed) return;
        _authState.Value = AuthStateType.Authenticating;

        try
        {
            _logger.LogInformation("Setting up api client");
            _apiClient.SetupApiClient();
            _logger.LogInformation("Setting up live client");
            await _backendHubManager.SetupLiveClient();
            _logger.LogInformation("Starting live client");
            await _hubClient.StartAsync();

            _logger.LogInformation("Refreshing shockers");
            await _apiClient.RefreshHubs();

            await _liveControlManager.RefreshConnections();

            var selfResponse = await _apiClient.Client!.GetSelf();
            if (!selfResponse.IsT0) throw new Exception("Failed to get self response");
            SelfResponse = selfResponse.AsT0.Value;

            _authState.Value = AuthStateType.Authed;
        }
        catch (Exception)
        {
            _authState.Value = AuthStateType.FailedAuth;
        }
        finally
        {
            _authLock.Release();
        }
    }

    public async Task Logout()
    {
        await _authLock.WaitAsync();
        
        if (_authState.Value != AuthStateType.Authed) return;

        try
        {
            _logger.LogInformation("Logging out");
            _configManager.Config.OpenShock.Token = string.Empty;
            await _configManager.SaveNow();
            
            await _hubClient.StopAsync();
            _apiClient.Logout();
            await _liveControlManager.RefreshConnections();

            _logger.LogInformation("Logged out");
        }
        finally
        {
            _authState.Value = AuthStateType.NotAuthed;
            _authLock.Release();
        }
    }
}