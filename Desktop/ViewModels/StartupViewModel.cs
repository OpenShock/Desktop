using CommunityToolkit.Mvvm.ComponentModel;
using OpenShock.Desktop.Services;

namespace OpenShock.Desktop.ViewModels;

/// <summary>The splash shown while <see cref="StartupService"/> brings the app up.</summary>
public sealed partial class StartupViewModel : ViewModelBase
{
    private readonly StartupService _startupService;

    [ObservableProperty] public partial string StepName { get; set; } = "Starting";
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial double ProgressMax { get; set; } = 1;
    [ObservableProperty] public partial bool IsIndeterminate { get; set; } = true;
    [ObservableProperty] public partial bool IsStarted { get; set; }

    public StartupViewModel(StartupService startupService)
    {
        _startupService = startupService;
        Update();

        Track(startupService.Status.Updated.SubscribeAsync(_ => OnUiAsync(Update)));
        Track(startupService.IsStarted.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Update)));
    }

    private void Update()
    {
        var status = _startupService.Status;
        StepName = status.StepName;
        IsIndeterminate = status.ProgressTotal is null;
        // Without a total the Blazor page showed a full bar, not an empty one.
        ProgressMax = status.ProgressTotal is { } total and > 0 ? total : 1;
        Progress = status.ProgressTotal is null ? 1 : status.ProgressCurrent;
        IsStarted = _startupService.IsStarted.Value;
    }
}
