#if AVALONIA
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.FileProviders;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Ui;

namespace OpenShock.Desktop.Platforms.AvaloniaShell;

/// <summary>
/// Serves the Blazor UI with Blazor Server on loopback, for the <see cref="MainWindow"/> webview to show.
/// </summary>
/// <remarks>
/// Avalonia's NativeWebView cannot answer requests itself (it can only edit request headers), so the
/// BlazorWebView approach of serving app:// from memory is not available. A real HTTP server is.
/// <para/>
/// Anything on the machine can reach a loopback port, and a web page can too through DNS rebinding,
/// and this UI controls shockers. Every request, the Blazor websocket included, therefore has to carry
/// the exact Host and a per-launch secret cookie. The webview gets the cookie by opening
/// <see cref="EntryUri"/> once; the cookie is HttpOnly, so page script never sees the secret.
/// </remarks>
public sealed class BlazorServerHost : IAsyncDisposable
{
    private const string CookieName = "openshock_session";

    private readonly string _secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public WebApplication App { get; }

    public IServiceProvider Services => App.Services;

    /// <summary>Only valid after <see cref="StartAsync"/>.</summary>
    public Uri BaseAddress { get; private set; } = null!;

    /// <summary>Hands the webview its session cookie, then redirects to <paramref name="path"/>.</summary>
    public Uri EntryUri(string path = "/") =>
        new(BaseAddress, $"/_session?t={_secret}&to={Uri.EscapeDataString(path)}");

    public BlazorServerHost(bool isDebug)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // The working directory is wherever we were launched from (autostart, URI handler, ...).
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
        });

        // Serilog is registered by AddOpenShockDesktopServices, the default providers would only duplicate it.
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(options => options.DetailedErrors = isDebug)
            // Loopback only, so there is no reason to keep the internet-facing 32 KB default.
            .AddHubOptions(options => options.MaximumReceiveMessageSize = 4 * 1024 * 1024);

        builder.Services.AddOpenShockDesktopServices();
        builder.Services.AddCommonBlazorServices();
        builder.Services.AddSingleton<ITrayService, AvaloniaTrayService>();

        // In a build output (not a publish) _framework and _content only exist as static web assets.
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

        App = builder.Build();

        App.Use(GuardAsync);

        var files = new CompositeFileProvider(App.Environment.WebRootFileProvider,
            new ModuleFileProvider(App.Services.GetRequiredService<ModuleManager.ModuleManager>()));
        App.UseStaticFiles(new StaticFileOptions { FileProvider = files });
        App.UseAntiforgery();
        // _framework/blazor.web.js is only served by the static asset endpoints, UseStaticFiles never sees it.
        App.MapStaticAssets();
        App.MapRazorComponents<ServerHostPage>().AddInteractiveServerRenderMode();
    }

    public async Task StartAsync()
    {
        await App.StartAsync();
        var address = App.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses
            .Single();
        BaseAddress = new Uri(address);
    }

    /// <summary>Whether <paramref name="uri"/> points at this host, as opposed to the outside world.</summary>
    public bool IsOwnOrigin(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp && uri.Host == BaseAddress.Host && uri.Port == BaseAddress.Port;

    private async Task GuardAsync(HttpContext context, RequestDelegate next)
    {
        // DNS rebinding: a page on attacker.example resolving to 127.0.0.1 still sends its own Host.
        if (!string.Equals(context.Request.Host.Value, $"127.0.0.1:{context.Connection.LocalPort}",
                StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (context.Request.Path == "/_session")
        {
            if (!SecretMatches(context.Request.Query["t"].ToString()))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.Response.Cookies.Append(CookieName, _secret, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true
            });

            // Local paths only, never an open redirect.
            var to = context.Request.Query["to"].ToString();
            context.Response.Redirect(to.StartsWith('/') && !to.StartsWith("//") && !to.StartsWith("/\\") ? to : "/");
            return;
        }

        if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie) || !SecretMatches(cookie))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private bool SecretMatches(string candidate) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(_secret));

    public async ValueTask DisposeAsync() => await App.DisposeAsync();
}
#endif
