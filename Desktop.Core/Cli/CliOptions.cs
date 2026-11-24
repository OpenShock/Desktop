using CommandLine;

namespace OpenShock.Desktop.Cli;

public class CliOptions
{
    [Option("headless", Required = false, Default = false, HelpText = "Run the application in headless mode.")]
    public required bool Headless { get; init; }

    [Option("uri", Required = false, HelpText = "Custom URI for callbacks (e.g. openshock:token/...)")]
    public string? Uri { get; init; }
}