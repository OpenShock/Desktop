using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.ModuleBase.StableInterfaces;
using OpenShock.Desktop.Theming;
using OpenShock.SDK.CSharp.Models;
using ControlType = OpenShock.SDK.CSharp.Models.ControlType;

namespace OpenShock.Desktop.ViewModels.Pages;

/// <summary>The user's shockers and the ones shared with them. A port of the Blazor ShockersTab.</summary>
public sealed partial class ShockersPageViewModel : PageViewModel
{
    /// <summary>
    /// Fallback for the (not expected) case where a shared shocker is missing from the permission map: showing
    /// nothing granted is the safe reading.
    /// </summary>
    private static readonly ShockerPermissions NoPermissions = new()
    {
        Shock = false,
        Vibrate = false,
        Sound = false,
        Live = false
    };

    private readonly OpenShockApi _api;
    private readonly BackendHubManager _hubManager;

    public ShockersPageViewModel(PageContext context) : base(context)
    {
        _api = Get<OpenShockApi>();
        _hubManager = Get<BackendHubManager>();

        Refresh();
        Track(_api.Hubs.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_api.SharedOwners.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
    }

    [ObservableProperty] public partial string Description { get; set; } = string.Empty;
    [ObservableProperty] public partial IReadOnlyList<ShockerRowViewModel> OwnShockers { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<SharedShockerGroup> SharedGroups { get; set; } = [];
    [ObservableProperty] public partial bool HasSharedShockers { get; set; }
    [ObservableProperty] public partial bool HasNoShockers { get; set; }

    private void Refresh()
    {
        var hubs = _api.Hubs.Value;
        var ownedCount = hubs.Sum(x => x.Shockers.Count);
        Description = $"{ownedCount} shockers across {hubs.Count} hubs";

        OwnShockers = hubs.SelectMany(x => x.Shockers).Select(x => new ShockerRowViewModel(x, null, _api)).ToArray();
        HasNoShockers = OwnShockers.Count == 0;

        SharedGroups = _api.SharedOwners.Value
            .SelectMany(owner => owner.Hubs
                .Where(hub => hub.Shockers.Count > 0)
                .Select(hub => new SharedShockerGroup($"{owner.Name} - {hub.Name}", hub.Shockers
                    .Select(shocker => new ShockerRowViewModel(shocker,
                        _api.SharedShockerPermissions.GetValueOrDefault(shocker.Id, NoPermissions), _api))
                    .ToArray())))
            .ToArray();
        HasSharedShockers = SharedGroups.Count > 0;
    }

    [RelayCommand]
    private Task TestShockers()
    {
        // Owned shockers only: a blanket test should never reach into someone else's hub.
        var commands = _api.Hubs.Value.SelectMany(x => x.Shockers)
            .Select(x => new Control
            {
                Id = x.Id,
                Duration = 1000,
                Intensity = 50,
                Type = ControlType.Vibrate,
                Exclusive = true
            });
        return _hubManager.Control(commands);
    }
}

/// <summary>Shared shockers of one owner's hub, under an "owner - hub" heading.</summary>
public sealed record SharedShockerGroup(string Title, IReadOnlyList<ShockerRowViewModel> Shockers);

public sealed partial class ShockerRowViewModel : ObservableObject
{
    private readonly IOpenShockShocker _shocker;
    private readonly OpenShockApi _api;

    /// <param name="permissions">
    /// What we may do with a shocker someone else shared with us, or null for our own. Shared shockers show their
    /// permissions instead of a pause toggle, since pausing another user's shocker is not ours to do.
    /// </param>
    public ShockerRowViewModel(IOpenShockShocker shocker, ShockerPermissions? permissions, OpenShockApi api)
    {
        _shocker = shocker;
        _api = api;
        IsEnabled = api.IsShockerEnabled(shocker.Id);

        if (permissions is not null)
            Permissions =
            [
                new PermissionIcon(MaterialIcons.Bolt, "Shock", permissions.Shock),
                new PermissionIcon(MaterialIcons.Vibration, "Vibrate", permissions.Vibrate),
                new PermissionIcon(MaterialIcons.VolumeUp, "Sound", permissions.Sound),
                new PermissionIcon(MaterialIcons.Sensors, "Live control", permissions.Live)
            ];
    }

    public string Name => _shocker.Name;
    public string Id => _shocker.Id.ToString();
    public bool IsPaused => _shocker.IsPaused;

    public IReadOnlyList<PermissionIcon>? Permissions { get; }
    public bool IsShared => Permissions is not null;
    public bool ShowPausedIcon => IsShared && IsPaused;
    public bool ShowPauseToggle => !IsShared && !IsChanging;
    public Geometry PauseIcon => IsPaused ? MaterialIcons.PlayArrow : MaterialIcons.Pause;
    public string PauseTooltip => IsPaused ? "Shocker Paused" : "Shocker Not Paused";

    [ObservableProperty] public partial bool IsEnabled { get; set; }
    [ObservableProperty] public partial bool IsChanging { get; set; }

    partial void OnIsEnabledChanged(bool value) => _api.SetShockerEnabled(_shocker.Id, value);

    partial void OnIsChangingChanged(bool value) => OnPropertyChanged(nameof(ShowPauseToggle));

    [RelayCommand]
    private async Task TogglePause()
    {
        if (_api.Client is null || IsChanging) return;
        IsChanging = true;

        try
        {
            // The hub list refreshes after this, which rebuilds the rows with the new state.
            await _api.Client.PauseShocker(_shocker.Id, !_shocker.IsPaused);
        }
        finally
        {
            IsChanging = false;
        }
    }
}

public sealed record PermissionIcon(Geometry Icon, string Label, bool Granted)
{
    public string Tooltip => $"{Label}: {(Granted ? "granted" : "not granted")}";
    public IBrush Brush => Granted ? ThemeBrushes.Success : ThemeBrushes.Dark;
}
