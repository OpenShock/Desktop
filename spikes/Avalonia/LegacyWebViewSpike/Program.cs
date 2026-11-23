using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using MudBlazor.Services;
using OpenShock.Spike.LegacyWebView.Components;

namespace OpenShock.Spike.LegacyWebView;

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
    private const string CookieName = "openshock_legacy";
    private static int _failures;

    private static void Check(string name, bool ok, string detail = "")
    {
        if (!ok) _failures++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -- " + detail : "")}");
    }

    private static void Info(string name, string detail) => Console.WriteLine($"INFO  {name}  -- {detail}");

    [STAThread]
    public static int Main(string[] args)
    {
        var outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "out");
        Directory.CreateDirectory(outDir);

        // ---- 1. Legacy Blazor host on loopback, before the UI thread starts ----
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var web = BuildLegacyHost(secret);
        web.StartAsync().GetAwaiter().GetResult();
        var address = new Uri(web.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.Single());
        Console.WriteLine($"legacy host listening on {address}");

        // ---- 2. Avalonia shell ----
        return AppBuilder.Configure<SpikeApp>().UsePlatformDetect().WithInterFont()
            .StartWithClassicDesktopLifetime(args, lifetime =>
            {
                lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                lifetime.Startup += (_, _) =>
                {
                    var window = BuildWindow(out var webView, out var overlay, out var popup);
                    lifetime.MainWindow = window;
                    window.Opened += async (_, _) =>
                    {
                        try
                        {
                            await RunChecks(address, secret, window, webView, overlay, popup, outDir);
                        }
                        catch (Exception e)
                        {
                            Check("no unexpected exception", false, e.ToString());
                        }

                        Console.WriteLine(_failures == 0 ? "ALL CHECKS PASSED" : $"{_failures} CHECK(S) FAILED");
                        await web.StopAsync();
                        lifetime.Shutdown(_failures);
                    };
                };
            });
    }

    private static WebApplication BuildLegacyHost(string secret)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development, // static web assets from the build output
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddMudServices();

        var app = builder.Build();

        // Loopback is reachable by every local process and, through DNS rebinding, by web pages.
        // Require the exact Host and a per-launch secret cookie on everything, websocket included.
        app.Use(async (ctx, next) =>
        {
            var local = ctx.Connection.LocalPort;
            if (!string.Equals(ctx.Request.Host.Value, $"127.0.0.1:{local}", StringComparison.Ordinal))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            if (ctx.Request.Path == "/_auth")
            {
                if (!CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(ctx.Request.Query["t"].ToString()),
                        System.Text.Encoding.UTF8.GetBytes(secret)))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                ctx.Response.Cookies.Append(CookieName, secret, new CookieOptions
                {
                    HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true
                });
                var to = ctx.Request.Query["to"].ToString();
                ctx.Response.Redirect(to.StartsWith('/') && !to.StartsWith("//") ? to : "/");
                return;
            }

            if (!ctx.Request.Cookies.TryGetValue(CookieName, out var cookie) || cookie != secret)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next();
        });

        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }

    private static Window BuildWindow(out NativeWebView webView, out Border overlay, out Popup popup)
    {
        webView = new NativeWebView();
        overlay = new Border
        {
            // Same window, drawn above the webview by Avalonia: the airspace question.
            Background = Brushes.Red, Width = 160, Height = 100, IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(60, 0, 0, 0),
            Child = new TextBlock { Text = "in-window overlay", Foreground = Brushes.White }
        };
        popup = new Popup
        {
            PlacementTarget = webView, Placement = PlacementMode.Center, HorizontalOffset = 200,
            Child = new Border
            {
                Background = Brushes.Blue, Width = 160, Height = 100,
                Child = new TextBlock { Text = "Avalonia popup", Foreground = Brushes.White }
            }
        };

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children =
            {
                new TextBlock { Text = "Avalonia shell (native)", Margin = new Thickness(12), FontSize = 18 },
                webView, overlay, popup
            }
        };
        Grid.SetRow(webView, 1);
        Grid.SetRow(overlay, 1);
        Grid.SetRow(popup, 1);

        return new Window
        {
            Title = "Legacy Blazor in NativeWebView spike", Width = 900, Height = 600, Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = root
        };
    }

    private static async Task RunChecks(Uri address, string secret, Window window, NativeWebView webView,
        Border overlay, Popup popup, string outDir)
    {
        // ---- loopback guard, from outside the webview ----
        using (var http = new HttpClient())
        {
            var noCookie = await http.GetAsync(new Uri(address, "/legacy/counter"));
            Check("request without the cookie is rejected", noCookie.StatusCode == HttpStatusCode.Unauthorized,
                ((int)noCookie.StatusCode).ToString());

            var badHost = new HttpRequestMessage(HttpMethod.Get, new Uri(address, "/legacy/counter"));
            badHost.Headers.Host = "attacker.example";
            badHost.Headers.Add("Cookie", $"{CookieName}={secret}");
            var rebinding = await http.SendAsync(badHost);
            Check("request with a foreign Host header is rejected (DNS rebinding)",
                rebinding.StatusCode == HttpStatusCode.Forbidden, ((int)rebinding.StatusCode).ToString());

            var wrongSecret = await http.GetAsync(new Uri(address, "/_auth?t=nope&to=/legacy/counter"));
            Check("auth endpoint rejects a wrong secret", wrongSecret.StatusCode == HttpStatusCode.Unauthorized,
                ((int)wrongSecret.StatusCode).ToString());
        }

        // ---- JS -> host messaging: record whatever arrives ----
        var messages = new List<string>();
        webView.WebMessageReceived += (_, e) => messages.Add(e.Body ?? "<null>");

        // ---- load the legacy page through the auth bootstrap ----
        webView.Source = new Uri(address, $"/_auth?t={secret}&to=/legacy/counter");

        var rendered = await WaitFor(async () => await Js(webView, "document.getElementById('count')?.textContent ?? ''") == "0",
            TimeSpan.FromSeconds(30));
        Check("legacy Blazor page rendered inside NativeWebView", rendered);
        if (!rendered)
        {
            Info("document", await Js(webView, "document.documentElement.outerHTML.slice(0, 400)"));
            return;
        }

        // The prerendered DOM shows up before the stylesheet has finished loading, so wait for it.
        const string radiusJs = "getComputedStyle(document.querySelector('.mud-paper')).borderRadius";
        await WaitFor(async () => await Js(webView, radiusJs) is not ("" or "0px"), TimeSpan.FromSeconds(10));
        var radius = await Js(webView, radiusJs);
        Check("MudBlazor static assets served (_content/MudBlazor css applied)", radius is not ("" or "0px"), $"mud-paper border-radius={radius}");

        var cookieVisible = await Js(webView, "document.cookie");
        Check("auth cookie is HttpOnly (not visible to page JS)", !cookieVisible.Contains(secret), $"document.cookie='{cookieVisible}'");

        // Waits for the interactive circuit first: the button only works once the websocket is up.
        var interactive = await WaitFor(async () =>
        {
            await Js(webView, "document.getElementById('inc').click()");
            return await WaitFor(async () => await Js(webView, "document.getElementById('count').textContent") != "0",
                TimeSpan.FromMilliseconds(500));
        }, TimeSpan.FromSeconds(20));
        Check("click round-trips through the Blazor Server circuit (websocket behind the cookie)", interactive,
            await Js(webView, "document.getElementById('count').textContent"));

        // ---- host -> page navigation without a reload ----
        await Js(webView, "window.__marker = 'kept'; Blazor.navigateTo('/legacy/second'); 'ok'");
        var navigated = await WaitFor(async () => await Js(webView, "document.getElementById('second') ? 'yes' : 'no'") == "yes",
            TimeSpan.FromSeconds(10));
        var marker = await Js(webView, "window.__marker ?? 'gone'");
        Check("host navigates the legacy view via Blazor.navigateTo, same circuit, no reload",
            navigated && marker == "kept", $"navigated={navigated} marker={marker}");

        // ---- page -> host messaging ----
        await Js(webView, """
            (function () {
              try { if (window.invokeCSharpAction) invokeCSharpAction('ping:invokeCSharpAction'); } catch (e) {}
              try { if (window.chrome && chrome.webview) chrome.webview.postMessage('ping:chrome.webview'); } catch (e) {}
              return 'sent';
            })()
            """);
        await WaitFor(() => Task.FromResult(messages.Count > 0), TimeSpan.FromSeconds(3));
        await Task.Delay(300);
        Check("page can message the host (WebMessageReceived)", messages.Count > 0, string.Join(", ", messages));

        // ---- airspace: what can Avalonia draw over the native webview? ----
        overlay.IsVisible = true;
        popup.IsOpen = true;
        await Task.Delay(1000);

        if (OperatingSystem.IsWindows())
        {
            var shot = Capture(window, Path.Combine(outDir, "webview-window.png"));
            var overlayPx = SampleCenter(shot, overlay, window);
            var popupPx = SampleCenter(shot, (Control)popup.Child!, window, popup);
            Info("in-window overlay over webview", IsColor(overlayPx, 255, 0, 0) ? "VISIBLE (drawn over webview)" : $"HIDDEN behind webview (pixel {overlayPx})");
            Info("Avalonia Popup over webview", IsColor(popupPx, 0, 0, 255) ? "VISIBLE (drawn over webview)" : $"HIDDEN behind webview (pixel {popupPx})");
            Console.WriteLine($"screenshot saved to {Path.Combine(outDir, "webview-window.png")}");
        }
    }

    // ---------------- helpers ----------------

    private static async Task<string> Js(NativeWebView webView, string script)
    {
        var raw = await webView.InvokeScript(script) ?? "";
        // WebView2 hands back JSON ("\"0\""); other engines may return the bare string.
        if (raw.StartsWith('"'))
        {
            try { return JsonSerializer.Deserialize<string>(raw) ?? ""; }
            catch (JsonException) { }
        }

        return raw == "null" ? "" : raw;
    }

    private static async Task<bool> WaitFor(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            try
            {
                if (await condition()) return true;
            }
            catch
            {
                // page not ready yet
            }

            await Task.Delay(150);
        }

        return false;
    }

    private sealed record Shot(System.Drawing.Bitmap Bitmap, PixelPoint Origin);

    private static Shot Capture(Window window, string path)
    {
        var origin = window.PointToScreen(new Point(0, 0));
        var scale = window.RenderScaling;
        var w = (int)(window.ClientSize.Width * scale);
        var h = (int)(window.ClientSize.Height * scale);
        var bmp = new System.Drawing.Bitmap(w, h);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(origin.X, origin.Y, 0, 0, new System.Drawing.Size(w, h));
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return new Shot(bmp, origin);
    }

    private static System.Drawing.Color SampleCenter(Shot shot, Control control, Window window, Popup? popup = null)
    {
        var centre = control.PointToScreen(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2));
        var x = Math.Clamp(centre.X - shot.Origin.X, 0, shot.Bitmap.Width - 1);
        var y = Math.Clamp(centre.Y - shot.Origin.Y, 0, shot.Bitmap.Height - 1);
        return shot.Bitmap.GetPixel(x, y);
    }

    private static bool IsColor(System.Drawing.Color c, int r, int g, int b) =>
        Math.Abs(c.R - r) < 30 && Math.Abs(c.G - g) < 30 && Math.Abs(c.B - b) < 30;
}
