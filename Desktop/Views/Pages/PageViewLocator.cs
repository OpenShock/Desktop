using Avalonia.Controls;
using Avalonia.Controls.Templates;
using OpenShock.Desktop.ViewModels.Pages;

namespace OpenShock.Desktop.Views.Pages;

/// <summary>The view for each built-in page.</summary>
public sealed class PageViewLocator : IDataTemplate
{
    public bool Match(object? data) => data is PageViewModel;

    public Control? Build(object? param) => param switch
    {
        DashboardPageViewModel => new DashboardPage(),
        ShockersPageViewModel => new ShockersPage(),
        ModulesPageViewModel => new ModulesPage(),
        SettingsPageViewModel => new SettingsPage(),
        LogsPageViewModel => new LogsPage(),
        DebugPageViewModel => new DebugPage(),
        LoginPageViewModel => new LoginPage(),
        ModulePageViewModel => new ModulePage(),
        _ => new TextBlock { Text = $"No view for {param?.GetType().Name}" }
    };
}
