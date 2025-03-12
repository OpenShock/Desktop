using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using OpenShock.SDK.CSharp.Live;
using OpenShock.SDK.CSharp.Live.LiveControlModels;
using Color = MudBlazor.Color;

namespace OpenShock.Desktop.Ui.Pages.Dash.Components;

public partial class StatePart : ComponentBase, IAsyncDisposable
{
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public required IOpenShockLiveControlClient Client { get; set; }
    
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public required string Text { get; set; }

    
    private Task StateOnValueChanged(WebsocketConnectionState state)
    {
        return InvokeAsync(StateHasChanged);
    }
    
    private Color GetConnectionStateColor() =>
        Client.State.Value switch
        {
            WebsocketConnectionState.Connected => Color.Success,
            WebsocketConnectionState.Reconnecting => Color.Warning,
            WebsocketConnectionState.Connecting => Color.Warning,
            WebsocketConnectionState.Disconnected => Color.Error,
            _ => Color.Error
        };


    protected override async Task OnInitializedAsync()
    {
        _stateSubscription = await Client.State.Updated.SubscribeAsync(_ => InvokeAsync(StateHasChanged));
        _latencySubscription = await Client.Latency.Updated.SubscribeAsync(_ => InvokeAsync(StateHasChanged));
    }

    private Task LatencyOnValueChanged(ulong arg)
    {
        return InvokeAsync(StateHasChanged);
    }

    private bool _disposed = false;
    private IAsyncDisposable _stateSubscription = null!;
    private IAsyncDisposable _latencySubscription = null!;
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        await _stateSubscription.DisposeAsync();
        await _latencySubscription.DisposeAsync();
    }
}