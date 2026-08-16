using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using LucHeart.WebsocketLibrary;
using OpenShock.SDK.CSharp.Live;
using Color = MudBlazor.Color;

namespace OpenShock.Desktop.Ui.Pages.Dash.Components;

public partial class StatePart : ComponentBase, IAsyncDisposable
{
    /// <summary>
    /// The active live control client for this hub, or null when no live control connection is open.
    /// Because connections are opened lazily and closed when idle, this switches between null and a client over time.
    /// </summary>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public IOpenShockLiveControlClient? Client { get; set; }

    /// <summary>
    /// Whether the hub is reported online by the backend (independent of whether a live control socket is open).
    /// </summary>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool Online { get; set; }

    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public required string Text { get; set; }

    /// <summary>
    /// Whether this hub is shared with the current user by another owner (as opposed to owned).
    /// </summary>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool Shared { get; set; }

    private bool _disposed;
    private IOpenShockLiveControlClient? _subscribedClient;
    private IAsyncDisposable? _stateSubscription;
    private IAsyncDisposable? _latencySubscription;

    private Color GetConnectionStateColor()
    {
        if (Client != null)
            return Client.State.Value switch
            {
                WebsocketConnectionState.Connected => Color.Success,
                WebsocketConnectionState.Connecting => Color.Warning,
                WebsocketConnectionState.WaitingForReconnect => Color.Warning,
                _ => Color.Error
            };

        return Online ? Color.Info : Color.Dark;
    }

    protected override async Task OnParametersSetAsync()
    {
        // The Client parameter switches between null and a client as connections open / close. Keep our subscriptions
        // pointed at the current client.
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
