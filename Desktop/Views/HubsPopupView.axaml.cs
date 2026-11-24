using Avalonia;
using Avalonia.Controls;
using OpenShock.Desktop.ViewModels;

namespace OpenShock.Desktop.Views;

public partial class HubsPopupView : UserControl
{
    public HubsPopupView()
    {
        InitializeComponent();
    }

    // The popup content is only in the visual tree while the popup is open, which is exactly when the latencies
    // need refreshing.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is HubsIndicatorViewModel viewModel) viewModel.IsOpen = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (DataContext is HubsIndicatorViewModel viewModel) viewModel.IsOpen = false;
    }
}
