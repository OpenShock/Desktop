using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenShock.Desktop.Services;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.Desktop.ViewModels.Pages;

/// <summary>Internal state and diagnostics, worth including in a bug report. A port of the Blazor DebugTab.</summary>
public sealed partial class DebugPageViewModel : PageViewModel
{
    private readonly AuthService _authService;
    private readonly ModuleManager.ModuleManager _moduleManager;

    public DebugPageViewModel(PageContext context) : base(context)
    {
        _authService = Get<AuthService>();
        _moduleManager = Get<ModuleManager.ModuleManager>();

        Refresh();
        Track(_authService.AuthState.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_moduleManager.ModulesLoaded.Subscribe(() => OnUi(Refresh)));
    }

    public string Version => Constants.Version.ToString();
    public string Runtime => RuntimeInformation.RuntimeIdentifier;

    [ObservableProperty] public partial IReadOnlyList<string> TokenPermissions { get; set; } = [];
    [ObservableProperty] public partial string? TokenPermissionsEmpty { get; set; }
    [ObservableProperty] public partial IReadOnlyList<string> RequiredPermissions { get; set; } = [];
    [ObservableProperty] public partial string? RequiredPermissionsEmpty { get; set; }

    private void Refresh()
    {
        var token = _authService.TokenSelf;
        TokenPermissions = token?.Permissions.Select(Name).ToArray() ?? [];
        TokenPermissionsEmpty = token is null ? "No token loaded." : TokenPermissions.Count == 0 ? "None granted." : null;

        RequiredPermissions = _moduleManager.RequiredPermissions.Select(Name).ToArray();
        RequiredPermissionsEmpty = RequiredPermissions.Count == 0 ? "No module requires a permission." : null;
    }

    private static string Name(PermissionType permission) => PermissionTypeBindings.PermissionTypeToName[permission].Name;
}
