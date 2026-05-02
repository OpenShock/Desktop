using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TUnit.Core;
using TUnit.Core.Exceptions;

namespace OpenShock.Desktop.ModuleManager.Tests;

public class MuslDetectionTests
{
    [Test]
    public async Task DetectRuntimeRid_OnNonLinux_ReturnsRuntimeIdentifier()
    {
        if (OperatingSystem.IsLinux())
            throw new SkipTestException("Only meaningful off Linux.");

        var detected = ModuleAssemblyLoadContext.DetectRuntimeRid();
        await Assert.That(detected).IsEqualTo(RuntimeInformation.RuntimeIdentifier);
    }

    [Test]
    public async Task DetectRuntimeRid_OnGlibcLinux_DoesNotEmitMuslPrefix()
    {
        if (!OperatingSystem.IsLinux())
            throw new SkipTestException("Linux-only.");
        if (HostHasMuslLoader())
            throw new SkipTestException("Host is musl — opposite of what this asserts.");

        var detected = ModuleAssemblyLoadContext.DetectRuntimeRid();
        await Assert.That(detected).DoesNotContain("linux-musl");
    }

    [Test]
    public async Task DetectRuntimeRid_OnMuslLinux_EmitsMuslPrefix()
    {
        if (!OperatingSystem.IsLinux())
            throw new SkipTestException("Linux-only.");
        if (!HostHasMuslLoader())
            throw new SkipTestException("Host is glibc — only meaningful on Alpine/musl.");

        var detected = ModuleAssemblyLoadContext.DetectRuntimeRid();
        await Assert.That(detected).StartsWith("linux-musl-");
    }

    private static bool HostHasMuslLoader()
    {
        try
        {
            if (Directory.Exists("/lib") &&
                Directory.EnumerateFiles("/lib", "ld-musl-*").Any()) return true;
            if (Directory.Exists("/lib64") &&
                Directory.EnumerateFiles("/lib64", "ld-musl-*").Any()) return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return false;
    }
}
