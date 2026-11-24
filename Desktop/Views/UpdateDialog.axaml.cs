using Avalonia.Controls;
using OpenShock.Desktop.ViewModels;

namespace OpenShock.Desktop.Views;

public partial class UpdateDialog : Window
{
    public UpdateDialog()
    {
        InitializeComponent();
    }

    public UpdateDialog(UpdateDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += Close;
        Closed += async (_, _) => await viewModel.DisposeAsync();
    }
}
