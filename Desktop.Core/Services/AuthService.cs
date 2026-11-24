using System.Net.Sockets;
using System.Text.Json;
using OpenShock.SDK.CSharp;
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
    private readonly ModuleManager.ModuleManager _moduleManager;
    public SelfResponse? SelfResponse { get; private set; } 
    public TokenResponse? TokenSelf { get; private set; }

    // NotAuthed
    // FailedAuth
    // Authed
    
    public bool MissingPermissions { get; private set; }

    /// <summary>
    /// Why the last authentication attempt failed. <see cref="AuthFailureReason.None"/> unless
    /// <see cref="AuthState"/> is <see cref="AuthStateType.FailedAuth"/>.
    /// </summary>
    public AuthFailureReason FailureReason { get; private set; } = AuthFailureReason.None;

    /// <summary>
    /// A message for the user explaining <see cref="FailureReason"/>, null when there is no failure.
    /// </summary>
    public string? FailureMessage { get; private set; }
    
    public IObservableVariable<AuthStateType> AuthState => _authState;
    private readonly ObservableVariable<AuthStateType> _authState = new(AuthStateType.NotAuthed);

    public AuthService(ILogger<AuthService> logger,
        BackendHubManager backendHubManager,
        OpenShockHubClient hubClient,
        LiveControlManager liveControlManager,
        OpenShockApi apiClient,
        ConfigManager configManager,
        ModuleManager.ModuleManager moduleManager)
    {
        _logger = logger;
        _backendHubManager = backendHubManager;
        _hubClient = hubClient;
        _liveControlManager = liveControlManager;
        _apiClient = apiClient;
        _configManager = configManager;
        _moduleManager = moduleManager;
    }

    private readonly SemaphoreSlim _authLock = new(1, 1);
    
    public async Task Authenticate()
    {
        await _authLock.WaitAsync();

        try
        {
            if (_authState.Value == AuthStateType.Authed) return;
            _authState.Value = AuthStateType.Authenticating;

            _logger.LogInformation("Setting up api client");
            _apiClient.SetupApiClient();
            _logger.LogInformation("Setting up live client");
            await _backendHubManager.SetupLiveClient();
            _logger.LogInformation("Starting live client");
            await _hubClient.StartAsync();

            _logger.LogInformation("Refreshing shockers");
            await _apiClient.RefreshAllHubs();

            await _liveControlManager.RefreshConnections();

            var selfResponse = await _apiClient.Client!.GetSelf();
            if (!selfResponse.IsT0)
            {
                Fail(AuthFailureReason.Unauthorized, TokenRejectedMessage);
                return;
            }

            SelfResponse = selfResponse.AsT0.Value;

            var tokenSelf = await _apiClient.Client!.GetTokenSelf();
            if (!tokenSelf.IsT0)
            {
                Fail(AuthFailureReason.Unauthorized, TokenRejectedMessage);
                return;
            }

            TokenSelf = tokenSelf.AsT0.Value;

            FailureReason = AuthFailureReason.None;
            FailureMessage = null;
            _authState.Value = AuthStateType.Authed;

            MissingPermissions = !_moduleManager.RequiredPermissions.Concat(Constants.BasePermissions).All(x => TokenSelf.Permissions.Contains(x));
            if (MissingPermissions)
            {
                _logger.LogWarning("Missing permissions for modules: {MissingPermissions}", 
                    string.Join(", ", _moduleManager.RequiredPermissions
                        .Concat(Constants.BasePermissions)
                        .Where(x => !TokenSelf.Permissions.Contains(x))
                        .Select(x => PermissionTypeBindings.PermissionTypeToName[x].Name)));
            }
        }
        catch (Exception ex)
        {
            var (reason, message) = Classify(ex);
            Fail(reason, message, ex);
        }
        finally
        {
            _authLock.Release();
        }
    }

    private const string TokenRejectedMessage =
        "Your API token was rejected. Log in again to get a new one.";

    /// <summary>
    /// Records why authentication failed, so the UI can tell the user something more useful than
    /// "Login Failed" and point them at the fix.
    /// </summary>
    private void Fail(AuthFailureReason reason, string message, Exception? exception = null)
    {
        FailureReason = reason;
        FailureMessage = message;
        _authState.Value = AuthStateType.FailedAuth;

        if (exception is null) _logger.LogError("Failed to authenticate: {Message}", message);
        else _logger.LogError(exception, "Failed to authenticate: {Message}", message);
    }

    /// <summary>
    /// Turns an exception from the login path into something a user can act on.
    /// </summary>
    private static (AuthFailureReason Reason, string Message) Classify(Exception exception) => exception switch
    {
        // The backend describes something (a permission, an enum value, a field) that this build
        // does not know about yet. Deserialization blows up somewhere deep in the SDK and there is
        // nothing the user can do except update.
        JsonException or KeyNotFoundException or NotSupportedException or InvalidCastException =>
            (AuthFailureReason.IncompatibleServer,
                "The server sent data this version of OpenShock Desktop does not understand. Please update to the latest version."),

        HttpRequestException or SocketException or TaskCanceledException or TimeoutException =>
            (AuthFailureReason.Unreachable,
                "Could not reach the OpenShock backend. Check your internet connection and the configured server."),

        OpenShockApiError apiError => (AuthFailureReason.Backend,
            $"The OpenShock backend returned an error: {apiError.Message}"),

        _ => (AuthFailureReason.Unknown, $"Login failed: {exception.Message}")
    };

    public async Task Logout()
    {
        await _authLock.WaitAsync();

        try
        {
            if (_authState.Value != AuthStateType.Authed) return;

            try
            {
                _logger.LogInformation("Logging out");
                _configManager.Config.OpenShock.Token = string.Empty;
                await _configManager.SaveNow();

                await _hubClient.StopAsync();
                _apiClient.Logout();
                await _liveControlManager.RefreshConnections();

                TokenSelf = null;

                _logger.LogInformation("Logged out");
            }
            finally
            {
                // Whatever happened on the way out, we are no longer logged in.
                FailureReason = AuthFailureReason.None;
                FailureMessage = null;
                _authState.Value = AuthStateType.NotAuthed;
            }
        }
        finally
        {
            _authLock.Release();
        }
    }
}