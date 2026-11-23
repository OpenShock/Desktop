using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenShock.Desktop.ModuleBase;

namespace OpenShock.Spike.AvaloniaModule.Views;

public partial class ModulePage : UserControl
{
    // Set by the host after construction, the same way Blazor components get theirs today.
    [ModuleInject] private SpikeService Service { get; set; } = null!;

    private readonly ModulePageViewModel _vm = new();

    public ModulePage()
    {
        InitializeComponent();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        // Property injection happens after the constructor, so read injected services from here on.
        _vm.Greeting = Service.Greeting;
        DataContext = _vm;
    }

    private void OnClick(object? sender, RoutedEventArgs e) => _vm.Clicks++;
}

public sealed class ModulePageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Greeting { get; set => Set(ref field, value); } = "";
    public int Clicks { get; set => Set(ref field, value); }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
