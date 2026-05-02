using TUnit.Core;

namespace OpenShock.Desktop.ModuleManager.Tests;

public class ManagedRidChainTests
{
    [Test]
    public async Task WinX64_WalksWinThenAnyCpu()
    {
        var chain = ModuleAssemblyLoadContext.BuildManagedRidChain("win-x64");
        await Assert.That(chain).IsEquivalentTo(["win-x64", "win", ""]);
    }

    [Test]
    public async Task LinuxX64_WalksLinuxThenUnixThenAnyCpu()
    {
        var chain = ModuleAssemblyLoadContext.BuildManagedRidChain("linux-x64");
        await Assert.That(chain).IsEquivalentTo(
            ["linux-x64", "linux", "unix-x64", "unix", ""]);
    }

    [Test]
    public async Task LinuxMuslX64_PrependsMuslThenFallsBackToGlibcChain()
    {
        var chain = ModuleAssemblyLoadContext.BuildManagedRidChain("linux-musl-x64");
        await Assert.That(chain).IsEquivalentTo(
            ["linux-musl-x64", "linux-musl", "linux-x64", "linux", "unix-x64", "unix", ""]);
    }

    [Test]
    public async Task LinuxBionicArm64_PrependsBionicThenLinuxThenUnix()
    {
        var chain = ModuleAssemblyLoadContext.BuildManagedRidChain("linux-bionic-arm64");
        await Assert.That(chain).IsEquivalentTo(
            ["linux-bionic-arm64", "linux-bionic", "linux-arm64", "linux", "unix-arm64", "unix", ""]);
    }

    [Test]
    public async Task OsxArm64_WalksOsxThenUnixThenAnyCpu()
    {
        var chain = ModuleAssemblyLoadContext.BuildManagedRidChain("osx-arm64");
        await Assert.That(chain).IsEquivalentTo(
            ["osx-arm64", "osx", "unix-arm64", "unix", ""]);
    }

    [Test]
    public async Task MaccatalystX64_WalksCatalystThenOsxThenUnix()
    {
        var chain = ModuleAssemblyLoadContext.BuildManagedRidChain("maccatalyst-x64");
        await Assert.That(chain).IsEquivalentTo(
            ["maccatalyst-x64", "maccatalyst", "osx-x64", "osx", "unix-x64", "unix", ""]);
    }

    [Test]
    public async Task FreebsdX64_WalksFreebsdThenUnix()
    {
        var chain = ModuleAssemblyLoadContext.BuildManagedRidChain("freebsd-x64");
        await Assert.That(chain).IsEquivalentTo(
            ["freebsd-x64", "freebsd", "unix-x64", "unix", ""]);
    }

    [Test]
    [Arguments("win-x64", "win", "x64")]
    [Arguments("linux-x64", "linux", "x64")]
    [Arguments("linux-musl-x64", "linux-musl", "x64")]
    [Arguments("linux-bionic-arm64", "linux-bionic", "arm64")]
    [Arguments("osx-arm64", "osx", "arm64")]
    [Arguments("maccatalyst-x64", "maccatalyst", "x64")]
    [Arguments("freebsd-x64", "freebsd", "x64")]
    [Arguments("linux-loongarch64", "linux", "loongarch64")]
    public async Task SplitRid_ParsesOsAndArch(string rid, string expectedOs, string expectedArch)
    {
        var (os, arch) = ModuleAssemblyLoadContext.SplitRid(rid);
        await Assert.That(os).IsEqualTo(expectedOs);
        await Assert.That(arch).IsEqualTo(expectedArch);
    }

    [Test]
    public async Task NativeRidChain_ContainsOnlySpecificRid_NoParentFallback()
    {
        var chain = ModuleAssemblyLoadContext.BuildNativeRidChain("linux-musl-x64");
        await Assert.That(chain).IsEquivalentTo(["linux-musl-x64"]);
    }
}
