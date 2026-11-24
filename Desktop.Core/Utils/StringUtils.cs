using OpenShock.SDK.CSharp.Models;

namespace OpenShock.Desktop.Utils;

public static class StringUtils
{
    public static string Truncate(this string input, int maxLength) => input[..Math.Min(maxLength, input.Length)];
}