using OpenShock.Desktop.Cli.Uri;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using UriParser = OpenShock.Desktop.Cli.Uri.UriParser;

namespace OpenShock.Desktop.ModuleManager.Tests;

/// <summary>
/// The deep link parser runs on the startup path against a string handed to us by the OS, so the
/// point of these is as much "does not throw" as "parses what the login flow sends".
/// </summary>
public sealed class UriParserTests
{
    [Test]
    public async Task ParsesTokenCallback()
    {
        var parsed = UriParser.TryParse("openshock:token/abc123");

        await Assert.That(parsed).IsNotNull();
        await Assert.That(parsed!.Type).IsEqualTo(UriParameterType.Token);
        await Assert.That(string.Join('/', parsed.Arguments)).IsEqualTo("abc123");
    }

    [Test]
    [Arguments("openshock://token/abc123")]
    [Arguments("openshock:/token/abc123")]
    [Arguments("OpenShock:Token/abc123")]
    [Arguments("  openshock:token/abc123  ")]
    public async Task AcceptsTheFormsTheOsHandsOver(string uri)
    {
        var parsed = UriParser.TryParse(uri);

        await Assert.That(parsed).IsNotNull();
        await Assert.That(parsed!.Type).IsEqualTo(UriParameterType.Token);
        await Assert.That(string.Join('/', parsed.Arguments)).IsEqualTo("abc123");
    }

    [Test]
    public async Task DecodesPercentEscapedArguments()
    {
        var parsed = UriParser.TryParse("openshock:token/a%2Bb%3Dc");

        await Assert.That(parsed).IsNotNull();
        await Assert.That(string.Join('/', parsed!.Arguments)).IsEqualTo("a+b=c");
    }

    [Test]
    public async Task ParsesShowWithoutArguments()
    {
        var parsed = UriParser.TryParse("openshock:show");

        await Assert.That(parsed).IsNotNull();
        await Assert.That(parsed!.Type).IsEqualTo(UriParameterType.Show);
        await Assert.That(parsed.Arguments.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("openshock:")]
    [Arguments("openshock:/")]
    [Arguments("openshock")]
    [Arguments("https://openshock.app/token/abc")]
    [Arguments("openshock:nonsense/abc")]
    [Arguments("openshock:1/abc")]
    public async Task ReturnsNullInsteadOfThrowing(string uri)
    {
        await Assert.That(UriParser.TryParse(uri)).IsNull();
    }

    [Test]
    public async Task ReturnsNullForNull()
    {
        await Assert.That(UriParser.TryParse(null)).IsNull();
    }
}
