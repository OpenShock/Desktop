using System.Diagnostics;

namespace OpenShock.Desktop.Utils;

public static class UiUtils
{
    public static string? TruncateAtWord(this string? input, int length)
    {
        if (input == null || input.Length < length)
            return input;

        var iNextSpace = input.LastIndexOf(" ", length, StringComparison.InvariantCultureIgnoreCase);

        return $"{input[..(iNextSpace > 0 ? iNextSpace : length)].Trim()}...";
    }
    
    public static string? TruncateAtChar(this string? input, int length)
    {
        if (input == null || input.Length < length)
            return input;
        
        var max = Math.Min(input.Length, length);

        return $"{input[..max].Trim()}...";
    }
    
    /// <summary>Opens <paramref name="url"/> in the user's default browser.</summary>
    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}