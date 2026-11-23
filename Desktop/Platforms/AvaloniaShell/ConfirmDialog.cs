#if AVALONIA
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace OpenShock.Desktop.Platforms.AvaloniaShell;

/// <summary>
/// A minimal yes/no dialog. It is a separate window on purpose: anything drawn inside the main window
/// ends up behind the native webview.
/// </summary>
public static class ConfirmDialog
{
    public static Task<bool> ShowAsync(Window owner, string title, string message, string confirm, string cancel)
    {
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.Background
        };

        var confirmButton = new Button { Content = confirm, IsDefault = true, MinWidth = 90 };
        var cancelButton = new Button { Content = cancel, IsCancel = true, MinWidth = 90 };
        confirmButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 20,
            MinWidth = 320,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { confirmButton, cancelButton }
                }
            }
        };

        return dialog.ShowDialog<bool>(owner);
    }
}
#endif
