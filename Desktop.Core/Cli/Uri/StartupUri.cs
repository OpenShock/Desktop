namespace OpenShock.Desktop.Cli.Uri;

/// <summary>
/// Carries the deep link this process was launched with into the host, for the case where this
/// process is the one that stays running.
/// <para>
/// Forwarding over the named pipe only covers a launch while another instance is already up. A
/// cold start (nothing running, the OS launches us for an <c>openshock:</c> link) parsed the
/// <c>--uri</c> argument and then dropped it, so the token never arrived and the user had to
/// start the login over again.
/// </para>
/// </summary>
public static class StartupUri
{
    private static UriParameter? _pending;

    /// <summary>
    /// Records the <c>--uri</c> value this process was started with, if it is one we understand.
    /// </summary>
    public static void Capture(string? uri) => _pending = UriParser.TryParse(uri);

    /// <summary>
    /// Takes the pending deep link, leaving nothing behind for a later caller.
    /// </summary>
    public static UriParameter? Consume() => Interlocked.Exchange(ref _pending, null);
}
