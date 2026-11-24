using CommandLine;

namespace OpenShock.Desktop.Cli;

/// <summary>
/// Options for the desktop (windowed) build.
/// </summary>
public sealed class DesktopCliOptions : CliOptions
{
    [Option('c', "console", Required = false, Default = false, HelpText = "Create/attach console window for stdout/stderr. This is very buggy since we only attach to it...")]
    public required bool Console { get; init; }
}