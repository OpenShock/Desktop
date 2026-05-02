using System.IO;
using TUnit.Core;

namespace OpenShock.Desktop.ModuleManager.Tests;

public class ManagedResolutionTests
{
    [Test]
    public async Task WinX64_PicksWinFolder_NotAnyCpuRoot()
    {
        // System.IO.Ports ships at libs/win/<dll> (parent RID, AnyCPU managed) and the
        // root has the platform-neutral facade. On Windows, win must beat the root.
        var module = FixtureLayout.CreateTempModule([
            "System.IO.Ports.dll",
            "win/System.IO.Ports.dll",
            "unix/System.IO.Ports.dll",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "win-x64");
        var resolved = alc.TryResolveManagedAssemblyPath("System.IO.Ports");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(module, "libs", "win", "System.IO.Ports.dll"));
    }

    [Test]
    public async Task LinuxX64_PicksUnixFolder_OverFacadeAtRoot()
    {
        var module = FixtureLayout.CreateTempModule([
            "System.IO.Ports.dll",
            "win/System.IO.Ports.dll",
            "unix/System.IO.Ports.dll",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-x64");
        var resolved = alc.TryResolveManagedAssemblyPath("System.IO.Ports");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(module, "libs", "unix", "System.IO.Ports.dll"));
    }

    [Test]
    public async Task LinuxMuslX64_FallsThroughToUnix_WhenOnlyUnixManagedExists()
    {
        // Real-world layout: musl-specific managed isn't published. Resolution must walk
        // linux-musl-x64 -> linux-musl -> linux-x64 -> linux -> unix-x64 -> unix -> root.
        var module = FixtureLayout.CreateTempModule([
            "System.IO.Ports.dll",
            "unix/System.IO.Ports.dll",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-musl-x64");
        var resolved = alc.TryResolveManagedAssemblyPath("System.IO.Ports");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(module, "libs", "unix", "System.IO.Ports.dll"));
    }

    [Test]
    public async Task RootLevelManagedAssembly_ResolvesViaAnyCpuFallback()
    {
        var module = FixtureLayout.CreateTempModule([
            "CircularBuffer.dll",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-x64");
        var resolved = alc.TryResolveManagedAssemblyPath("CircularBuffer");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(module, "libs", "CircularBuffer.dll"));
    }

    [Test]
    public async Task MissingAssembly_ReturnsNull_DefersToParentContext()
    {
        var module = FixtureLayout.CreateTempModule([]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-x64");
        var resolved = alc.TryResolveManagedAssemblyPath("Nonexistent.Assembly");

        await Assert.That(resolved).IsNull();
    }

    [Test]
    public async Task SpecificRidWins_OverParentFolders()
    {
        // If the publisher does ship a linux-x64 specific managed (rare), it should beat
        // the parent linux/unix variants.
        var module = FixtureLayout.CreateTempModule([
            "linux-x64/Foo.dll",
            "linux/Foo.dll",
            "unix/Foo.dll",
            "Foo.dll",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-x64");
        var resolved = alc.TryResolveManagedAssemblyPath("Foo");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(module, "libs", "linux-x64", "Foo.dll"));
    }
}
