using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LucHeart.WebsocketLibrary;
using OpenShock.Desktop.Models;
using OpenShock.Desktop.ModuleBase.StableInterfaces;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Theming;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.Desktop.ViewModels;

/// <summary>
/// A set of hubs as one row in the sidebar, with the detail in a popup. The rail is 250px wide, so a row per hub does
/// not fit; the dashboard is where they are all written out.
/// </summary>
public sealed partial class HubsIndicatorViewModel : ObservableObject
{
    private readonly Func<IReadOnlyList<HubGroup>> _groups;
    private readonly LiveControlManager _liveControlManager;

    // Latency ticks several times a second and only shows inside the popup, so poll while it is open instead of
    // subscribing to every client.
    private readonly DispatcherTimer _refreshWhileOpen;

    public HubsIndicatorViewModel(string label, string popoverTitle, Geometry icon, Func<IReadOnlyList<HubGroup>> groups,
        LiveControlManager liveControlManager)
    {
        Label = label;
        PopoverTitle = popoverTitle;
        Icon = icon;
        _groups = groups;
        _liveControlManager = liveControlManager;
        _refreshWhileOpen = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background,
            (_, _) => Refresh());
    }

    public string Label { get; }
    public string PopoverTitle { get; }
    public Geometry Icon { get; }

    [ObservableProperty] public partial bool HasHubs { get; set; }
    [ObservableProperty] public partial string CountText { get; set; } = "0/0";
    [ObservableProperty] public partial IBrush AggregateBrush { get; set; } = ThemeBrushes.Dark;
    [ObservableProperty] public partial bool IsOpen { get; set; }

    public ObservableCollection<HubListEntry> Entries { get; } = [];

    partial void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            Refresh();
            _refreshWhileOpen.Start();
        }
        else
        {
            _refreshWhileOpen.Stop();
        }
    }

    public void Refresh()
    {
        var groups = _groups();
        var hubs = groups.SelectMany(g => g.Hubs).ToArray();
        var online = hubs.Count(h => h.Status.Online);

        HasHubs = hubs.Length > 0;
        CountText = $"{online}/{hubs.Length}";

        if (hubs.Any(h => _liveControlManager.GetClient(h.Id)?.State.Value == WebsocketConnectionState.Connected))
            AggregateBrush = ThemeBrushes.Success;
        else
            AggregateBrush = online > 0 ? ThemeBrushes.Info : ThemeBrushes.Dark;

        var entries = new List<HubListEntry>();
        foreach (var group in groups)
        {
            if (group.Name is null)
            {
                entries.AddRange(group.Hubs.Select(hub => Row(hub, hub.Name, false)));
            }
            else if (group.Hubs.Count == 1)
            {
                // A single hub is redundant with the group, so collapse into one row.
                entries.Add(Row(group.Hubs[0], group.Name, false));
            }
            else
            {
                entries.Add(HubListEntry.Header(group.Name.Truncate(20)));
                entries.AddRange(group.Hubs.Select(hub => Row(hub, hub.Name, true)));
            }
        }

        // Update in place where the shape is unchanged, so an open popup does not flicker on every tick.
        if (entries.Count == Entries.Count)
        {
            for (var i = 0; i < entries.Count; i++)
                if (Entries[i] != entries[i]) Entries[i] = entries[i];
        }
        else
        {
            Entries.Clear();
            foreach (var entry in entries) Entries.Add(entry);
        }
    }

    private HubListEntry Row(IOpenShockHub hub, string name, bool indented)
    {
        var client = _liveControlManager.GetClient(hub.Id);

        IBrush dot;
        string state;
        if (client is null)
        {
            dot = hub.Status.Online ? ThemeBrushes.Info : ThemeBrushes.Dark;
            state = hub.Status.Online ? "idle" : "offline";
        }
        else
        {
            dot = client.State.Value switch
            {
                WebsocketConnectionState.Connected => ThemeBrushes.Success,
                WebsocketConnectionState.Connecting or WebsocketConnectionState.WaitingForReconnect => ThemeBrushes.Warning,
                _ => ThemeBrushes.Error
            };
            state = client.State.Value.ToString();
        }

        var latency = client is { State.Value: WebsocketConnectionState.Connected } ? $"{client.Latency.Value}ms" : null;

        return new HubListEntry(false, name.Truncate(indented ? 22 : 20), indented, dot, state, latency,
            client?.Gateway?.GetGatewayDisplayUrl());
    }
}

/// <summary>One line in the hubs popup: a group heading or a hub.</summary>
public sealed record HubListEntry(
    bool IsHeader,
    string Name,
    bool Indented,
    IBrush? DotBrush,
    string? State,
    string? Latency,
    string? Gateway)
{
    public static HubListEntry Header(string name) => new(true, name, false, null, null, null, null);

    public bool IsHub => !IsHeader;
    public bool HasLatency => Latency is not null;
    public bool HasGateway => Gateway is not null;
}
