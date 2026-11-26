using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.ModuleManager;
using OpenShock.Desktop.Modules;

namespace OpenShock.Desktop.ViewModels.Pages;

/// <summary>
/// A module's native (Avalonia) page, or a notice where a module page cannot be shown: the module is missing, failed
/// to start, or its page failed. Blazor module pages are not this; they are the webview.
/// </summary>
public sealed partial class ModulePageViewModel : PageViewModel
{
    private readonly LoadedModule? _module;
    private readonly Type? _viewType;
    private readonly string _title;
    private readonly ModuleExceptionGuard? _guard;
    private readonly ILogger<ModulePageViewModel> _logger;

    /// <summary>Shows the page <paramref name="viewType"/> of <paramref name="module"/>.</summary>
    public ModulePageViewModel(PageContext context, LoadedModule module, Type viewType, string title) : base(context)
    {
        _module = module;
        _viewType = viewType;
        _title = title;
        _logger = Get<ILogger<ModulePageViewModel>>();
        _guard = context.ModuleGuard;
        _guard.ModuleFaulted += OnModuleFaulted;

        Load();
    }

    private ModulePageViewModel(PageContext context, ModuleNotice notice) : base(context)
    {
        _title = notice.Title;
        _logger = Get<ILogger<ModulePageViewModel>>();
        Notice = notice;
    }

    public static ModulePageViewModel ForNotice(PageContext context, ModuleNotice notice) => new(context, notice);

    /// <summary>The module's page, null while <see cref="Notice"/> is shown instead.</summary>
    [ObservableProperty] public partial Control? View { get; private set; }

    [ObservableProperty] public partial ModuleNotice? Notice { get; private set; }

    public bool CanRetry => _viewType is not null;

    [RelayCommand]
    private void Retry() => Load();

    private void Load()
    {
        if (_module is null || _viewType is null) return;

        CloseView();
        try
        {
            View = ModuleControlFactory.Create(_module, _viewType, Context.Services);
            Notice = null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create page {Type} of module {ModuleId}", _viewType.FullName, _module.Id);
            Notice = new ModuleNotice($"{_title} could not be opened",
                "The page failed while it was being created. Updating or reinstalling the module usually fixes this.",
                e.ToString());
        }
    }

    private void OnModuleFaulted(LoadedModule module, Exception exception)
    {
        if (module != _module || View is null) return;

        // Out of the visual tree, so a page that throws while being laid out does not throw again on every frame.
        CloseView();
        Notice = new ModuleNotice($"{_title} ran into an error",
            "The page threw an exception and was closed so the rest of the app keeps running. Opening it again " +
            "usually works; if it keeps happening, check for an update of the module.",
            exception.ToString());
    }

    private void CloseView()
    {
        var view = View;
        View = null;

        try
        {
            (view as IDisposable)?.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Page {Type} of module {ModuleId} threw while being disposed", _viewType?.FullName,
                _module?.Id);
        }
    }

    public override ValueTask DisposeAsync()
    {
        if (_guard is not null) _guard.ModuleFaulted -= OnModuleFaulted;
        CloseView();
        return base.DisposeAsync();
    }
}

/// <param name="Details">The exception, for a bug report.</param>
public sealed record ModuleNotice(string Title, string Message, string? Details = null)
{
    public bool HasDetails => Details is not null;
}
