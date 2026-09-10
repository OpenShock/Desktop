namespace OpenShock.Desktop.Cli.Uri;

/// <summary>
/// Parser for the custom <c>openshock:</c> scheme registered with the OS (see the installer and
/// the AppImage desktop entry), used for deep links such as the token callback of the login flow.
/// </summary>
public static class UriParser
{
    private const string Scheme = "openshock:";

    /// <summary>
    /// Parses a deep link such as <c>openshock:token/abc</c>.
    /// </summary>
    /// <returns>
    /// The parsed request, or <c>null</c> when this is not a link we understand. These values come
    /// straight from the OS and land on the startup path, so anything malformed has to come back
    /// as null instead of throwing.
    /// </returns>
    public static UriParameter? TryParse(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return null;

        var value = uri.Trim();
        if (!value.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)) return null;

        // openshock:token/abc and openshock://token/abc are the same request to us. Browsers and
        // desktop environments are not consistent about which form they hand over.
        var rest = value[Scheme.Length..].TrimStart('/');
        if (rest.Length == 0) return null;

        var typeEnd = rest.IndexOf('/');
        var type = typeEnd == -1 ? rest : rest[..typeEnd];

        // Matched by name rather than through Enum.TryParse, which would also accept the numeric
        // values ("openshock:1/..." is not a link anything produces).
        var match = Enum.GetValues<UriParameterType>()
            .Cast<UriParameterType?>()
            .FirstOrDefault(x => string.Equals(x.ToString(), type, StringComparison.OrdinalIgnoreCase));
        if (match is not { } parameterType) return null;

        if (typeEnd == -1) return new UriParameter { Type = parameterType };

        var arguments = rest[(typeEnd + 1)..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(System.Uri.UnescapeDataString)
            .ToArray();

        return new UriParameter { Type = parameterType, Arguments = arguments };
    }
}
