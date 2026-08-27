using System.ComponentModel;
using LucHeart.WebsocketLibrary;
using Microsoft.AspNetCore.Components;
using OpenShock.Desktop.ModuleBase.StableInterfaces;
using OpenShock.SDK.CSharp.Live;
using Color = MudBlazor.Color;

namespace OpenShock.Desktop.Ui.Pages.Dash.Components;

public partial class HubStatusRow : ComponentBase, IAsyncDisposable
{
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public required IOpenShockHub Hub { get; set; }

    /// <summary>
    /// The active live control client for this hub, or null when no live control connection is open.
    /// Because connections are opened lazily and closed when idle, this switches between null and a
    /// client over time.
    /// </summary>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public IOpenShockLiveControlClient? Client { get; set; }

    /// <summary>Name of the owner, when this hub is shared with the user rather than theirs.</summary>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string? SharedBy { get; set; }

    private Color StateColor
    {
        get
        {
            if (Client != null)
                return Client.State.Value switch
                {
                    WebsocketConnectionState.Connected => Color.Success,
                    WebsocketConnectionState.Connecting => Color.Warning,
                    WebsocketConnectionState.WaitingForReconnect => Color.Warning,
                    _ => Color.Error
                };

            return Hub.Status.Online ? Color.Info : Color.Dark;
        }
    }

    private string StateLabel
    {
        get
        {
            if (Client == null) return Hub.Status.Online ? "idle" : "offline";

            return Client.State.Value switch
            {
                WebsocketConnectionState.Connected => "live",
                WebsocketConnectionState.Connecting => "connecting",
                WebsocketConnectionState.WaitingForReconnect => "reconnecting",
                _ => "offline"
            };
        }
    }

    /// <summary>The hub's live control latency, empty when there is no socket up.</summary>
    private string LatencyText => Client is { State.Value: WebsocketConnectionState.Connected }
        ? $"{Client.Latency.Value}ms"
        : string.Empty;

    private string? Gateway => Client?.Gateway;

    private bool _disposed;
    private IOpenShockLiveControlClient? _subscribedClient;
    private IAsyncDisposable? _stateSubscription;
    private IAsyncDisposable? _latencySubscription;

    protected override async Task OnParametersSetAsync()
    {
        // The Client parameter switches between null and a client as connections open / close. Keep
        // our subscriptions pointed at the current client.
        if (ReferenceEquals(_subscribedClient, Client)) return;

        if (_disposed) return;

        await UnsubscribeAsync();
        _subscribedClient = Client;

        if (Client == null) return;

        var stateSubscription = await Client.State.Updated.SubscribeAsync(_ => InvokeAsync(StateHasChanged));
        var latencySubscription = await Client.Latency.Updated.SubscribeAsync(_ => InvokeAsync(StateHasChanged));

        // Disposal can also land while those two awaits are in flight.
        if (_disposed)
        {
            await stateSubscription.DisposeAsync();
            await latencySubscription.DisposeAsync();
            return;
        }

        _stateSubscription = stateSubscription;
        _latencySubscription = latencySubscription;
    }

    private async Task UnsubscribeAsync()
    {
        if (_stateSubscription != null)
        {
            await _stateSubscription.DisposeAsync();
            _stateSubscription = null;
        }

        if (_latencySubscription != null)
        {
            await _latencySubscription.DisposeAsync();
            _latencySubscription = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await UnsubscribeAsync();
    }
}
