using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.Logging;
using OpenShock.Desktop.Theming;
using Serilog.Events;

namespace OpenShock.Desktop.ViewModels.Pages;

/// <summary>The in-app log console. A port of the Blazor LogsTab.</summary>
public sealed partial class LogsPageViewModel : PageViewModel
{
    // Log lines come in bursts (startup, a module going wrong), so they are picked up in batches.
    private static readonly TimeSpan BatchInterval = TimeSpan.FromMilliseconds(100);

    private readonly HashSet<LogStore.LogEntry> _shown = [];
    private readonly DispatcherTimer _batch;

    public LogsPageViewModel(PageContext context) : base(context)
    {
        Levels =
        [
            new LevelFilter(LogEventLevel.Verbose, Rebuild),
            new LevelFilter(LogEventLevel.Debug, Rebuild),
            new LevelFilter(LogEventLevel.Information, Rebuild),
            new LevelFilter(LogEventLevel.Warning, Rebuild),
            new LevelFilter(LogEventLevel.Error, Rebuild),
            new LevelFilter(LogEventLevel.Fatal, Rebuild)
        ];

        _batch = new DispatcherTimer(BatchInterval, DispatcherPriority.Background, (_, _) => PickUpNewLines());
        Rebuild();

        LogStore.OnLogAdded += OnLogAdded;
    }

    public IReadOnlyList<LevelFilter> Levels { get; }
    public ObservableCollection<LogLineViewModel> Lines { get; } = [];

    [ObservableProperty] public partial bool AutoScroll { get; set; } = true;

    public string AutoScrollText => AutoScroll ? "autoscroll: on" : "autoscroll: off";

    partial void OnAutoScrollChanged(bool value)
    {
        OnPropertyChanged(nameof(AutoScrollText));
        if (value) ScrollToEndRequested?.Invoke();
    }

    /// <summary>Raised when the view should scroll to the newest line.</summary>
    public event Action? ScrollToEndRequested;

    [RelayCommand]
    private void ToggleAutoScroll() => AutoScroll = !AutoScroll;

    [RelayCommand]
    private void Clear()
    {
        while (LogStore.Logs.TryDequeue(out _))
        {
        }

        Rebuild();
    }

    [RelayCommand]
    private static void OpenLogsFolder() =>
        Process.Start(new ProcessStartInfo { FileName = Constants.LogsFolder, UseShellExecute = true, Verb = "open" });

    /// <summary>Called on whatever thread logged.</summary>
    private void OnLogAdded() => OnUi(() =>
    {
        if (!_batch.IsEnabled) _batch.Start();
    });

    private void PickUpNewLines()
    {
        _batch.Stop();

        var logs = LogStore.Logs.ToArray();
        var current = new HashSet<LogStore.LogEntry>(logs);

        // The store keeps the last 1000, so the oldest lines drop off the front.
        while (Lines.Count > 0 && !current.Contains(Lines[0].Entry))
        {
            _shown.Remove(Lines[0].Entry);
            Lines.RemoveAt(0);
        }

        _shown.IntersectWith(current);

        var added = false;
        foreach (var entry in logs)
        {
            if (!_shown.Add(entry) || !IsShown(entry.Level)) continue;
            Lines.Add(new LogLineViewModel(entry));
            added = true;
        }

        if (added && AutoScroll) ScrollToEndRequested?.Invoke();
    }

    private void Rebuild()
    {
        Lines.Clear();
        _shown.Clear();

        foreach (var entry in LogStore.Logs.ToArray())
        {
            _shown.Add(entry);
            if (IsShown(entry.Level)) Lines.Add(new LogLineViewModel(entry));
        }

        if (AutoScroll) ScrollToEndRequested?.Invoke();
    }

    private bool IsShown(LogEventLevel level) => Levels.Any(x => x.Level == level && x.IsEnabled);

    public override ValueTask DisposeAsync()
    {
        LogStore.OnLogAdded -= OnLogAdded;
        _batch.Stop();
        return base.DisposeAsync();
    }

    public static string Tag(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "TRC",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        LogEventLevel.Fatal => "FTL",
        _ => "???"
    };

    public static IBrush LevelBrush(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => ThemeBrushes.TextDisabled,
        LogEventLevel.Debug => ThemeBrushes.TextSecondary,
        LogEventLevel.Information => ThemeBrushes.Info,
        LogEventLevel.Warning => ThemeBrushes.Warning,
        _ => ThemeBrushes.Error
    };
}

/// <summary>One of the level chips in the toolbar.</summary>
public sealed partial class LevelFilter : ObservableObject
{
    private readonly Action _changed;

    public LevelFilter(LogEventLevel level, Action changed)
    {
        Level = level;
        _changed = changed;
    }

    public LogEventLevel Level { get; }
    public string Tag => LogsPageViewModel.Tag(Level);
    public IBrush Brush => LogsPageViewModel.LevelBrush(Level);
    public bool IsFatal => Level == LogEventLevel.Fatal;

    [ObservableProperty] public partial bool IsEnabled { get; set; } = true;

    [RelayCommand]
    private void Toggle()
    {
        IsEnabled = !IsEnabled;
        _changed();
    }
}

public sealed partial class LogLineViewModel : ObservableObject
{
    public LogLineViewModel(LogStore.LogEntry entry)
    {
        Entry = entry;
        IsExpanded = entry.IsExpanded;
    }

    public LogStore.LogEntry Entry { get; }

    public string Time => Entry.Time.ToString("HH:mm:ss.fff");
    public string Tag => LogsPageViewModel.Tag(Entry.Level);
    public IBrush LevelBrush => LogsPageViewModel.LevelBrush(Entry.Level);
    public bool IsFatal => Entry.Level == LogEventLevel.Fatal;
    public string Source => Entry.SourceContextShort;

    /// <summary>Single line in the list; the full text is in <see cref="Details"/>.</summary>
    public string Message => Entry.Message.ReplaceLineEndings(" ");

    public string Details => $"{Entry.SourceContext}\n{Entry.Message}";

    [ObservableProperty] public partial bool IsExpanded { get; set; }

    // Kept on the entry, so a line stays open when the page is left and opened again.
    partial void OnIsExpandedChanged(bool value) => Entry.IsExpanded = value;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}
