using System.Collections.Concurrent;
using Avalonia.Media.Imaging;

namespace OpenShock.Desktop.Theming;

/// <summary>Downloads and caches small remote images, like the account avatar.</summary>
public static class RemoteImages
{
    private static readonly HttpClient HttpClient = CreateClient();
    private static readonly ConcurrentDictionary<Uri, Task<Bitmap?>> Cache = new();

    public static Task<Bitmap?> LoadAsync(Uri uri, ILogger logger) => Cache.GetOrAdd(uri, u => DownloadAsync(u, logger));

    private static async Task<Bitmap?> DownloadAsync(Uri uri, ILogger logger)
    {
        try
        {
            await using var stream = await HttpClient.GetStreamAsync(uri);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            return new Bitmap(buffer);
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Could not load image {Uri}", uri);
            Cache.TryRemove(uri, out _);
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
        return client;
    }
}
