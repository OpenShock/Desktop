using CommunityToolkit.Mvvm.ComponentModel;
using OpenShock.Desktop.Services;

namespace OpenShock.Desktop.ViewModels;

/// <summary>The main window: the startup splash until the app is up, then the sidebar and the page.</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    public ShellViewModel(IServiceProvider services)
    {
        Startup = new StartupViewModel(services.GetRequiredService<StartupService>());
        Sidebar = new SidebarViewModel(services);
        IsStarted = Startup.IsStarted;

        Startup.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StartupViewModel.IsStarted)) IsStarted = Startup.IsStarted;
        };
    }

    public StartupViewModel Startup { get; }
    public SidebarViewModel Sidebar { get; }

    [ObservableProperty] public partial bool IsStarted { get; set; }

    public override async ValueTask DisposeAsync()
    {
        await Startup.DisposeAsync();
        await Sidebar.DisposeAsync();
        await base.DisposeAsync();
    }
}
