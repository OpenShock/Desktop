using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleManager;

namespace OpenShock.Spike.Host;

public sealed class SpikeApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }
}

public static class Program
{
    private static int _failures;

    private static void Check(string name, bool ok, string detail = "")
    {
        if (!ok) _failures++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -- " + detail : "")}");
    }

    /// <summary>usage: SpikeHost [--window] module.zip workDir</summary>
    [STAThread]
    public static int Main(string[] args)
    {
        var window = args.Contains("--window");
        var rest = args.Where(a => a != "--window").ToArray();
        var zip = Path.GetFullPath(rest[0]);
        var work = Path.GetFullPath(rest[1]);

        // Same layout ModuleManager produces: modules/<id>/<module>.dll + libs/
        var moduleFolder = Path.Combine(work, "modules", "openshock.spike.avaloniamodule");
        if (Directory.Exists(moduleFolder)) Directory.Delete(moduleFolder, true);
        ZipFile.ExtractToDirectory(zip, moduleFolder);

        var builder = AppBuilder.Configure<SpikeApp>().WithInterFont();

        if (window)
        {
            return builder.UsePlatformDetect().StartWithClassicDesktopLifetime(rest, lifetime =>
            {
                lifetime.Startup += (_, _) =>
                {
                    lifetime.MainWindow = new Window
                    {
                        Title = "Avalonia module spike", Width = 600, Height = 420,
                        Content = CreateModulePage(LoadModule(moduleFolder))
                    };
                };
            });
        }

        builder.UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        try
        {
            RunChecks(moduleFolder, Path.Combine(work, "frame.png"));
        }
        catch (Exception e)
        {
            Check("no unexpected exception", false, e.ToString());
        }

        Console.WriteLine(_failures == 0 ? "ALL CHECKS PASSED" : $"{_failures} CHECK(S) FAILED");
        return _failures;
    }

    private sealed record Loaded(ModuleAssemblyLoadContext Alc, DesktopModuleBase Module, Type PageType);

    /// <summary>Mirrors ModuleManager.LoadModule.</summary>
    private static Loaded LoadModule(string moduleFolder)
    {
        var dll = Directory.GetFiles(moduleFolder, "*.dll", SearchOption.TopDirectoryOnly).Single();
        var alc = new ModuleAssemblyLoadContext(moduleFolder);
        var assembly = alc.LoadFromAssemblyPath(dll);
        var attribute = assembly.GetCustomAttribute<DesktopModuleAttribute>()
                        ?? throw new Exception("no DesktopModuleAttribute");
        var services = new ServiceCollection().BuildServiceProvider();
        var module = (DesktopModuleBase)ActivatorUtilities.CreateInstance(services, attribute.ModuleType);
        return new Loaded(alc, module, module.NavigationComponents.First().ComponentType);
    }

    /// <summary>Mirrors OpenShockModuleComponentActivator: construct, then fill [ModuleInject] properties.</summary>
    private static Control CreateModulePage(Loaded loaded)
    {
        var control = (Control)ActivatorUtilities.CreateInstance(new ServiceCollection().BuildServiceProvider(),
            loaded.PageType);
        foreach (var prop in loaded.PageType
                     .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     .Where(p => p.IsDefined(typeof(ModuleInjectAttribute), true) && p.CanWrite))
        {
            prop.SetValue(control, loaded.Module.ModuleServiceProvider.GetRequiredService(prop.PropertyType));
        }

        return control;
    }

    private static void RunChecks(string moduleFolder, string framePath)
    {
        var loaded = LoadModule(moduleFolder);

        // --- type identity / isolation ---
        Check("page type is an Avalonia Control as seen by the host",
            typeof(Control).IsAssignableFrom(loaded.PageType), loaded.PageType.FullName ?? "");
        Check("page type lives in the module ALC",
            AssemblyLoadContext.GetLoadContext(loaded.PageType.Assembly) == loaded.Alc);
        Check("Avalonia itself is the host's copy",
            AssemblyLoadContext.GetLoadContext(typeof(Control).Assembly) == AssemblyLoadContext.Default);

        // --- assets, resolved from the host before any module XAML ran ---
        var logoUri = new Uri("avares://OpenShock.Spike.AvaloniaModule/Assets/logo.png");
        Check("host can resolve module avares:// asset (cold)", AssetLoader.Exists(logoUri), logoUri.ToString());
        using (var s = AssetLoader.Open(logoUri))
            Check("host can open module asset as Bitmap", new Bitmap(s).PixelSize.Width == 64);

        // --- build + render the page ---
        var page = CreateModulePage(loaded);
        var window = new Window { Width = 600, Height = 420, Content = page };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        T Find<T>(string name) where T : Control =>
            page.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name)
            ?? throw new Exception($"control {name} not found");

        var assembliesInAlc = loaded.Alc.Assemblies.Select(a => a.GetName().Name).OrderBy(n => n).ToArray();
        Check("module ALC holds only module-private assemblies",
            assembliesInAlc.All(n => n is "OpenShock.Spike.AvaloniaModule" or "OpenShock.Spike.Helpers" or "Humanizer"),
            string.Join(", ", assembliesInAlc));

        var logo = Find<Image>("Logo");
        Check("<Image Source=avares://...> loaded", logo.Source is Bitmap { PixelSize.Width: 64 },
            logo.Source?.GetType().Name ?? "null");

        var title = Find<TextBlock>("Title");
        Check("StyleInclude from module applied",
            title.Foreground is ISolidColorBrush { Color: var c } && c == Color.Parse("#E14A6D") && title.FontSize == 24,
            $"{title.Foreground} / {title.FontSize}");

        var accent = Find<Border>("AccentBox");
        Check("merged ResourceInclude + DynamicResource resolved",
            accent.Background is ISolidColorBrush { Color: var a } && a == Color.Parse("#AFE3FE"),
            accent.Background?.ToString() ?? "null");

        var greeting = Find<TextBlock>("Greeting");
        Check("[ModuleInject] + compiled binding", greeting.Text?.Contains("[ModuleInject]") == true,
            greeting.Text ?? "null");

        var counter = Find<TextBlock>("Counter");
        Check("converter from module-private lib (Helpers -> Humanizer)", counter.Text == "clicked zero times",
            counter.Text ?? "null");

        // Real input through the headless platform, not RaiseEvent.
        var button = Find<Button>("ClickButton");
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Check("click handler + INotifyPropertyChanged update", counter.Text == "clicked one times",
            counter.Text ?? "null");

        var badge = page.GetVisualDescendants().OfType<TemplatedControl>().First(c => c.Name == "Badge");
        var badgeBorder = badge.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "PART_Border");
        Check("custom TemplatedControl + ControlTheme from module", badgeBorder is not null);

        // --- pixels: prove it actually rendered, not just built a tree ---
        var frame = window.CaptureRenderedFrame() ?? throw new Exception("no frame rendered");
        frame.Save(framePath);
        var accentPoint = accent.TranslatePoint(new Point(accent.Bounds.Width / 2, accent.Bounds.Height / 2), window)!
            .Value;
        var pixel = ReadPixel(frame, (int)accentPoint.X, (int)accentPoint.Y);
        Check("accent bar rendered with module color", pixel == Color.Parse("#AFE3FE"), pixel.ToString());
        Console.WriteLine($"frame saved to {framePath}");
    }

    private static unsafe Color ReadPixel(WriteableBitmap bitmap, int x, int y)
    {
        using var fb = bitmap.Lock();
        var p = (byte*)fb.Address + y * fb.RowBytes + x * 4;
        return fb.Format == PixelFormat.Rgba8888
            ? Color.FromArgb(p[3], p[0], p[1], p[2])
            : Color.FromArgb(p[3], p[2], p[1], p[0]);
    }
}
