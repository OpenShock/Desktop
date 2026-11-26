using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.Theming;
using OpenShock.Desktop.Utils;

namespace OpenShock.Desktop.ViewModels.Pages;

/// <summary>The Discord server card on the dashboard. A port of the Blazor DiscordInvite.</summary>
public sealed partial class DiscordInviteViewModel : ObservableObject
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, (DateTimeOffset Fetched, DiscordInviteResponse Invite)> Cache = new();
    private static readonly HttpClient HttpClient = new() { BaseAddress = new Uri("https://discord.com/api/v10/") };

    private readonly string _inviteCode;
    private readonly ILogger _logger;

    public DiscordInviteViewModel(string inviteCode, ILogger logger)
    {
        _inviteCode = inviteCode;
        _logger = logger;
        _ = LoadAsync();
    }

    [ObservableProperty] public partial bool IsLoading { get; set; } = true;
    [ObservableProperty] public partial bool IsLoaded { get; set; }
    [ObservableProperty] public partial bool IsFailed { get; set; }
    [ObservableProperty] public partial string GuildName { get; set; } = string.Empty;
    [ObservableProperty] public partial string OnlineText { get; set; } = string.Empty;
    [ObservableProperty] public partial string MembersText { get; set; } = string.Empty;
    [ObservableProperty] public partial Bitmap? Splash { get; set; }
    [ObservableProperty] public partial Bitmap? Icon { get; set; }

    public string Blurb => $"Join the {GuildName} Discord Server!".ToUpperInvariant();

    partial void OnGuildNameChanged(string value) => OnPropertyChanged(nameof(Blurb));

    [RelayCommand]
    private void Join() => UiUtils.OpenUrl($"https://discord.gg/{_inviteCode}");

    private async Task LoadAsync()
    {
        var invite = await GetInviteAsync();
        IsLoading = false;

        if (invite is null)
        {
            IsFailed = true;
            return;
        }

        GuildName = invite.Guild.Name;
        OnlineText = $"{invite.ApproximatePresenceCount} Online";
        MembersText = $"{invite.ApproximateMemberCount} Members";
        IsLoaded = true;

        var guild = invite.Guild;
        var splash = RemoteImages.LoadAsync(
            new Uri($"https://cdn.discordapp.com/splashes/{guild.Id}/{guild.Splash}?size=480"), _logger);
        var icon = RemoteImages.LoadAsync(new Uri($"https://cdn.discordapp.com/icons/{guild.Id}/{guild.Icon}"), _logger);
        Splash = await splash;
        Icon = await icon;
    }

    private async Task<DiscordInviteResponse?> GetInviteAsync()
    {
        if (Cache.TryGetValue(_inviteCode, out var cached) && DateTimeOffset.UtcNow - cached.Fetched < CacheFor)
            return cached.Invite;

        try
        {
            var response = await HttpClient.GetAsync($"invites/{_inviteCode}?with_counts=true");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch invite {InviteCode}. API returned {StatusCode}", _inviteCode,
                    response.StatusCode);
                return null;
            }

            var invite = await response.Content.ReadFromJsonAsync<DiscordInviteResponse>();
            if (invite is not null) Cache[_inviteCode] = (DateTimeOffset.UtcNow, invite);
            return invite;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load discord invite {InviteCode}", _inviteCode);
            return null;
        }
    }

    private sealed class DiscordInviteResponse
    {
        [JsonPropertyName("approximate_member_count")]
        public required uint ApproximateMemberCount { get; set; }

        [JsonPropertyName("approximate_presence_count")]
        public required uint ApproximatePresenceCount { get; set; }

        [JsonPropertyName("guild")] public required DiscordGuild Guild { get; set; }
    }

    private sealed class DiscordGuild
    {
        [JsonPropertyName("id")] public required ulong Id { get; set; }
        [JsonPropertyName("name")] public required string Name { get; set; }
        [JsonPropertyName("icon")] public required string Icon { get; set; }
        [JsonPropertyName("splash")] public required string Splash { get; set; }
    }
}
