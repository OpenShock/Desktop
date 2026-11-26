using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using OpenShock.Desktop.Theming;
using OpenShock.Desktop.ViewModels.Pages;

namespace OpenShock.Desktop.Platforms.AvaloniaShell;

/// <summary>
/// A yes/no dialog in the look of a MudDialog. It is a separate window on purpose: anything drawn inside the main
/// window ends up behind the webview when a module page is open.
/// </summary>
public static class ConfirmDialog
{
    public static Task<bool> ShowAsync(Window owner, ConfirmRequest request)
    {
        var dialog = new Window
        {
            Title = request.Title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = owner.Icon
        };
        dialog.Bind(Window.BackgroundProperty, dialog.GetResourceObservable("SurfaceBrush"));

        var confirmButton = new Button { Content = request.Confirm, IsDefault = true, Classes = { "text", "primary" } };
        var cancelButton = new Button { Content = request.Cancel, IsCancel = true, Classes = { "text" } };
        confirmButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        if (request.Icon is not null)
            title.Children.Add(new IconView { Data = request.Icon, VerticalAlignment = VerticalAlignment.Center });
        title.Children.Add(new TextBlock { Text = request.Title, Classes = { "h6" } });

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(new TextBlock { Text = request.Message, Classes = { "body2" }, TextWrapping = TextWrapping.Wrap });
        if (request.Warning is not null)
        {
            var warning = new TextBlock { Text = request.Warning, Classes = { "body2" }, TextWrapping = TextWrapping.Wrap };
            warning.Bind(TextElement.ForegroundProperty, warning.GetResourceObservable("WarningBrush"));
            body.Children.Add(warning);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24, 20, 16, 12),
            Spacing = 16,
            MinWidth = 360,
            MaxWidth = 520,
            Children =
            {
                title,
                body,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, confirmButton }
                }
            }
        };

        return dialog.ShowDialog<bool>(owner);
    }
}
