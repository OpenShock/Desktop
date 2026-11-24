using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Services;
using OpenShock.Internal.Common.Utils;

namespace OpenShock.Desktop.ViewModels;

/// <summary>The update prompt and download progress. A port of the Blazor UpdateDialog.</summary>
public sealed partial class UpdateDialogViewModel : ViewModelBase
{
    private readonly Updater _updater;
    private readonly ConfigManager _configManager;

    public UpdateDialogViewModel(Updater updater, ConfigManager configManager)
    {
        _updater = updater;
        _configManager = configManager;

        Version = updater.LatestReleaseInfo?.Version.ToString() ?? string.Empty;
        ReleaseNotes = updater.LatestReleaseInfo?.Response.Body ?? string.Empty;

        Track(updater.DownloadProgress.ValueUpdated.SubscribeAsync(progress => OnUiAsync(() =>
        {
            Progress = progress;
            IsDownloaded = Math.Abs(progress - 100) < 0.0001;
        })));
    }

    public string Version { get; }
    public string ReleaseNotes { get; }

    [ObservableProperty] public partial bool IsDownloading { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool IsDownloaded { get; set; }

    public bool IsPrompting => !IsDownloading;

    partial void OnIsDownloadingChanged(bool value) => OnPropertyChanged(nameof(IsPrompting));

    /// <summary>Raised when the dialog should close.</summary>
    public event Action? CloseRequested;

    [RelayCommand]
    private void Dismiss() => CloseRequested?.Invoke();

    [RelayCommand]
    private void Skip()
    {
        if (_updater.LatestReleaseInfo is { } release)
        {
            _configManager.Config.App.LastIgnoredVersion = release.Version;
            _configManager.Save();
        }

        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Update()
    {
        IsDownloading = true;
        // Downloads, starts the installer and exits.
        OsTask.Run(_updater.DoUpdate);
    }
}
