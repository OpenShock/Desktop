using System.Collections.Concurrent;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;

namespace OpenShock.Desktop.Theming;

/// <summary>Downloads and caches small remote images, like the account avatar and module icons.</summary>
public static class RemoteImages
{
    private static readonly HttpClient HttpClient = CreateClient();
    private static readonly ConcurrentDictionary<Uri, Task<Bitmap?>> Bitmaps = new();
    private static readonly ConcurrentDictionary<Uri, Task<IImage?>> Images = new();

    /// <summary>A raster image (PNG, JPEG, ...).</summary>
    public static Task<Bitmap?> LoadAsync(Uri uri, ILogger logger) =>
        Bitmaps.GetOrAdd(uri, u => DownloadAsync(u, logger, Bitmaps, bytes => new Bitmap(new MemoryStream(bytes))));

    /// <summary>A raster image or an SVG, told apart by content: module repositories link to either.</summary>
    public static Task<IImage?> LoadImageAsync(Uri uri, ILogger logger) =>
        Images.GetOrAdd(uri, u => DownloadAsync<IImage>(u, logger, Images, bytes => IsSvg(bytes)
            ? new SvgImage { Source = SvgSource.LoadFromStream(new MemoryStream(bytes)) }
            : new Bitmap(new MemoryStream(bytes))));

    private static async Task<T?> DownloadAsync<T>(Uri uri, ILogger logger,
        ConcurrentDictionary<Uri, Task<T?>> cache, Func<byte[], T> decode) where T : class
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(uri);
            return decode(bytes);
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Could not load image {Uri}", uri);
            // Not cached, so it is tried again next time.
            cache.TryRemove(uri, out _);
            return null;
        }
    }

    private static bool IsSvg(byte[] bytes)
    {
        var head = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart('﻿', ' ',
            '\t', '\r', '\n');
        return head.StartsWith('<');
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
        return client;
    }
}
