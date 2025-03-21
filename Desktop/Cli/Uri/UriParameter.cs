namespace OpenShock.Desktop.Cli.Uri;

public class UriParameter
{
    public required UriParameterType Type { get; set; }
    public IReadOnlyList<string> Arguments { get; set; } = Array.Empty<string>();
}