using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenShock.Desktop.ModuleBase;

namespace OpenShock.Desktop.Modules.ExampleModule;

/// <summary>
/// A native module page: any Avalonia <see cref="Control"/> can be a <c>NavigationItem.ComponentType</c>.
/// </summary>
/// <remarks>
/// Constructor parameters come from the module's services first, then the app's (the logger here is the app's).
/// <see cref="ModuleInjectAttribute"/> properties are filled from the module's services after construction, the same
/// as for Blazor pages. A page that implements <see cref="IDisposable"/> is disposed when the user leaves it.
/// </remarks>
public partial class NativePage : UserControl
{
    private readonly ILogger<NativePage> _logger;
    private int _count;

    /// <summary>For the XAML previewer only.</summary>
    public NativePage() : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<NativePage>.Instance)
    {
    }

    [ActivatorUtilitiesConstructor]
    public NativePage(ILogger<NativePage> logger)
    {
        _logger = logger;
        InitializeComponent();
    }

    [ModuleInject] private ExampleService ExampleService { get; set; } = null!;

    public string Greeting => ExampleService.GetExampleString();

    /// <summary>
    /// Bound from here rather than the constructor: <see cref="ModuleInjectAttribute"/> properties are only filled in
    /// once the constructor has run.
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        DataContext = this;
    }

    private void OnCount(object? sender, RoutedEventArgs e)
    {
        _count++;
        CountText.Text = $"Clicked {_count} times";
        _logger.LogInformation("Native page clicked {Count} times", _count);
    }

    private void OnThrow(object? sender, RoutedEventArgs e) =>
        throw new InvalidOperationException("Thrown on purpose by the example module's native page");
}

/// <summary>A native page whose module service cannot be constructed, like the Blazor "Broken Page".</summary>
public sealed class BrokenNativePage : UserControl
{
    [ModuleInject] private BrokenService BrokenService { get; set; } = null!;
}
