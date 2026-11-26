using Avalonia.Controls;
using Avalonia.Threading;
using OpenShock.Desktop.ViewModels.Pages;

namespace OpenShock.Desktop.Views.Pages;

public partial class LogsPage : UserControl
{
    private LogsPageViewModel? _viewModel;

    public LogsPage()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null) _viewModel.ScrollToEndRequested -= ScrollToEnd;
        _viewModel = DataContext as LogsPageViewModel;
        if (_viewModel is not null)
        {
            _viewModel.ScrollToEndRequested += ScrollToEnd;
            ScrollToEnd();
        }
    }

    // After the new lines are laid out, or the end is not the end yet.
    private void ScrollToEnd() => Dispatcher.UIThread.Post(() => Console.ScrollToEnd(), DispatcherPriority.Background);
}
