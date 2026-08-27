using System.IO.Pipes;
using System.Text.Json;
using OpenShock.Desktop.Cli.Uri;
using OpenShock.Desktop.Services.Pipes;
using UriParser = OpenShock.Desktop.Cli.Uri.UriParser;

namespace OpenShock.Desktop.Utils;

/// <summary>
/// Single instance enforcement for the whole application.
/// <para>
/// This deliberately does not use the named pipe to decide whether another instance is running:
/// the pipe only exists once the host has booted far enough to start
/// <see cref="PipeServerService"/>, which on a cold start can be a minute after launch. Any
/// launch inside that window saw no pipe and started a second full instance, and two live
/// instances fight over the module folder - one deletes module files the other has loaded.
/// A named mutex is owned from the very first line of Main instead, so the window is gone.
/// </para>
/// </summary>
public static class SingleInstanceGuard
{
    // Unprefixed: session scoped on Windows (what we want - the app is per user), and portable
    // to the Unix named mutex implementation, which rejects backslashes in names.
    private const string MutexName = "OpenShock.Desktop.SingleInstance";

    // Held for the lifetime of the process, released implicitly when it exits.
    private static Mutex? _mutex;

    /// <summary>
    /// Tries to become the primary instance. Returns false when another instance already owns it,
    /// in which case the caller should forward its request and exit.
    /// </summary>
    public static bool TryAcquire()
    {
        Mutex mutex;
        try
        {
            mutex = new Mutex(false, MutexName);
        }
        catch (Exception ex)
        {
            // Cannot create or open the mutex at all. Refusing to start would be worse than the
            // race we are guarding against, so fail open and run as primary.
            Console.WriteLine($"Failed to create single instance mutex, continuing anyway: {ex.Message}");
            return true;
        }

        bool acquired;
        try
        {
            acquired = mutex.WaitOne(TimeSpan.Zero, false);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner died without releasing it (crash or kill). It is ours now.
            acquired = true;
        }

        if (!acquired)
        {
            mutex.Dispose();
            return false;
        }

        _mutex = mutex;
        return true;
    }

    /// <summary>
    /// Hands the incoming URI (or a plain show request) to the already running instance.
    /// </summary>
    /// <remarks>
    /// Retries until <paramref name="timeout"/> elapses, because the instance holding the mutex
    /// may still be starting up and its pipe server may not be listening yet.
    /// </remarks>
    public static bool TryForwardToRunningInstance(string? uri, TimeSpan timeout)
    {
        var message = BuildMessage(uri);
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            try
            {
                using var pipeClientStream = new NamedPipeClientStream(".", Constants.PipeName, PipeDirection.Out);
                pipeClientStream.ConnectAsync(500);

                using var writer = new StreamWriter(pipeClientStream) { AutoFlush = true };
                writer.WriteLine(JsonSerializer.Serialize(message));

                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
            {
                if (DateTime.UtcNow >= deadline) return false;
                Thread.Sleep(250);
            }
        }
    }

    private static PipeMessage BuildMessage(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return new PipeMessage { Type = PipeMessageType.Show };

        var parsedUri = UriParser.Parse(uri);

        return parsedUri.Type switch
        {
            UriParameterType.Token => new PipeMessage
            {
                Type = PipeMessageType.Token, Data = string.Join('/', parsedUri.Arguments)
            },
            // Anything else (including an unparsable URI) just focuses the running instance.
            _ => new PipeMessage { Type = PipeMessageType.Show }
        };
    }
}
