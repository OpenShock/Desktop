using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Services.Pipes;
using OpenShock.Desktop.Ui;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Models;
using OpenShock.SDK.CSharp.Utils;

namespace OpenShock.Desktop.ViewModels.Pages;

/// <summary>Logging in to OpenShock, in the browser or with a token. A port of the Blazor LoginPage and LoginPart.</summary>
public sealed partial class LoginPageViewModel : PageViewModel
{
    private static readonly Uri ProductionServer = new("https://api.openshock.app/");

    private readonly ConfigManager _configManager;
    private readonly AuthService _authService;
    private readonly ILogger<LoginPageViewModel> _logger;

    public LoginPageViewModel(PageContext context) : base(context)
    {
        _configManager = Get<ConfigManager>();
        _authService = Get<AuthService>();
        _logger = Get<ILogger<LoginPageViewModel>>();

        Token = _configManager.Config.OpenShock.Token;
        var backend = _configManager.Config.OpenShock.Backend;
        RebuildServers(backend == ProductionServer ? null : backend);

        // A browser login hands the token back through the openshock:// URI, which reaches us over the pipe.
        Track(Get<PipeServerService>().OnTokenReceived.SubscribeAsync(TokenReceived));
    }

    // ===== Browser login =====

    [ObservableProperty] public partial bool IsOpeningBrowser { get; set; }

    [RelayCommand]
    private async Task LoginWithBrowser()
    {
        IsOpeningBrowser = true;
        try
        {
            using var httpClient = new HttpClient { BaseAddress = _configManager.Config.OpenShock.Backend };
            httpClient.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);

            var response = await httpClient.GetAsync("1");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to reach API, status code: {StatusCode}", response.StatusCode);
                Context.Notify("Failed to reach API", UiNotificationSeverity.Error);
                return;
            }

            var root = await response.Content.ReadFromJsonAsync<BaseResponse<RootResponse>>();
            if (root?.Data is null)
            {
                Context.Notify("Failed to reach API", UiNotificationSeverity.Error);
                return;
            }

            var permissions = string.Join(',', Constants.BasePermissions
                .Concat(Get<ModuleManager.ModuleManager>().RequiredPermissions)
                .Select(x => PermissionTypeBindings.PermissionTypeToName[x].Name));

            var requestUri = new Uri(root.Data.ShortLinkUrl,
                $"/t/?name=OpenShock%20Desktop&redirect_uri=openshock:token/%&permissions={permissions}");
            UiUtils.OpenUrl(requestUri.ToString());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to reach API");
            Context.Notify("Failed to reach API", UiNotificationSeverity.Error);
        }
        finally
        {
            IsOpeningBrowser = false;
        }
    }

    private async Task TokenReceived(string token)
    {
        _configManager.Config.OpenShock.Token = token;
        await _configManager.SaveNow();
        await ProceedAuthenticated();
    }

    // ===== Advanced: server and token =====

    [ObservableProperty] public partial bool IsAdvancedOpen { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ServerOption> Servers { get; set; } = [];
    [ObservableProperty] public partial ServerOption? SelectedServer { get; set; }
    [ObservableProperty] public partial bool IsEditingCustomServer { get; set; }
    [ObservableProperty] public partial string CustomServer { get; set; } = string.Empty;
    [ObservableProperty] public partial string Token { get; set; } = string.Empty;

    public bool IsCustomServerValid => ParseServer(CustomServer) is not null;

    partial void OnCustomServerChanged(string value) => OnPropertyChanged(nameof(IsCustomServerValid));

    partial void OnSelectedServerChanged(ServerOption? value)
    {
        if (value is not null) _configManager.Config.OpenShock.Backend = value.Url;
    }

    partial void OnTokenChanged(string value) => _configManager.Config.OpenShock.Token = value;

    [RelayCommand]
    private void ToggleAdvanced() => IsAdvancedOpen = !IsAdvancedOpen;

    [RelayCommand]
    private void EditCustomServer() => IsEditingCustomServer = true;

    [RelayCommand]
    private void CancelCustomServer() => IsEditingCustomServer = false;

    [RelayCommand]
    private void SaveCustomServer()
    {
        if (ParseServer(CustomServer) is not { } uri) return;

        RebuildServers(uri);
        SelectedServer = Servers[^1];
        IsEditingCustomServer = false;
    }

    [RelayCommand]
    private async Task ContinueWithToken()
    {
        _configManager.Save();
        await ProceedAuthenticated();
    }

    private void RebuildServers(Uri? custom)
    {
        var production = new ServerOption(ProductionServer, "OpenShock Official");
        Servers = custom is null ? [production] : [production, new ServerOption(custom, "Custom")];
        SelectedServer = Servers.FirstOrDefault(x => x.Url == _configManager.Config.OpenShock.Backend) ?? production;
    }

    private static Uri? ParseServer(string text) =>
        Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;

    private async Task ProceedAuthenticated()
    {
        Context.Navigate(Routes.Dashboard);
        await _authService.Authenticate();
    }

    private sealed class RootResponse
    {
        public required string Version { get; set; }
        public required string Commit { get; set; }
        public required DateTimeOffset CurrentTime { get; set; }
        public required Uri FrontendUrl { get; set; }
        public required Uri ShortLinkUrl { get; set; }
    }
}

/// <summary>A backend to log in to.</summary>
public sealed record ServerOption(Uri Url, string Label)
{
    public override string ToString() => $"{Url} ({Label})";
}
