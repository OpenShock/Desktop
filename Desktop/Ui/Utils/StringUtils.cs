namespace OpenShock.Desktop.Ui.Utils;

public static class StringUtils
{
    public static string Truncate(this string input, int maxLength) => input[..Math.Min(maxLength, input.Length)];
    
}