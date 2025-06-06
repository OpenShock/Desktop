using CommandLine;

namespace OpenShock.Desktop.Cli;

public sealed class MauiCliOptions : CliOptions
{
    [Option('c', "console", Required = false, Default = false, HelpText = "Create/attach console window for stdout/stderr. This is very buggy since we only attach to it...")]
    public required bool Console { get; init; }
    
    [Option("uri", Required = false, HelpText = "Custom URI for callbacks")]
    public required string? Uri { get; init; }
}