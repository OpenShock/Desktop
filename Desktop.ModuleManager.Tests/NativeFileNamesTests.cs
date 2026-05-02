using System.Linq;
using TUnit.Core;

namespace OpenShock.Desktop.ModuleManager.Tests;

public class NativeFileNamesTests
{
    [Test]
    public async Task Windows_BareName_AppendsDll()
    {
        var names = ModuleAssemblyLoadContext
            .EnumerateNativeFileNames("foo", ModuleAssemblyLoadContext.PlatformKind.Windows)
            .ToArray();
        await Assert.That(names).IsEquivalentTo(["foo", "foo.dll"]);
    }

    [Test]
    public async Task Windows_AlreadyHasDll_NotDoubled()
    {
        var names = ModuleAssemblyLoadContext
            .EnumerateNativeFileNames("foo.dll", ModuleAssemblyLoadContext.PlatformKind.Windows)
            .ToArray();
        await Assert.That(names).IsEquivalentTo(["foo.dll"]);
    }

    [Test]
    public async Task Apple_BareName_TriesLibPrefixAndDylibExt()
    {
        var names = ModuleAssemblyLoadContext
            .EnumerateNativeFileNames("foo", ModuleAssemblyLoadContext.PlatformKind.Apple)
            .ToArray();
        await Assert.That(names).IsEquivalentTo(["foo", "foo.dylib", "libfoo", "libfoo.dylib"]);
    }

    [Test]
    public async Task Apple_AlreadyLibPrefixed_NoDoubling()
    {
        var names = ModuleAssemblyLoadContext
            .EnumerateNativeFileNames("libfoo", ModuleAssemblyLoadContext.PlatformKind.Apple)
            .ToArray();
        await Assert.That(names).IsEquivalentTo(["libfoo", "libfoo.dylib"]);
    }

    [Test]
    public async Task Unix_BareName_TriesLibPrefixAndSoExt()
    {
        var names = ModuleAssemblyLoadContext
            .EnumerateNativeFileNames("foo", ModuleAssemblyLoadContext.PlatformKind.Unix)
            .ToArray();
        await Assert.That(names).IsEquivalentTo(["foo", "foo.so", "libfoo", "libfoo.so"]);
    }

    [Test]
    public async Task Unix_VersionedSoSuffix_DoesNotAppendAnotherSo()
    {
        // P/Invoke for [DllImport("libfoo.so.6")] must not produce "libfoo.so.6.so".
        var names = ModuleAssemblyLoadContext
            .EnumerateNativeFileNames("libfoo.so.6", ModuleAssemblyLoadContext.PlatformKind.Unix)
            .ToArray();
        await Assert.That(names).IsEquivalentTo(["libfoo.so.6"]);
    }

    [Test]
    public async Task Unix_PlainSoSuffix_NotDoubled()
    {
        var names = ModuleAssemblyLoadContext
            .EnumerateNativeFileNames("libfoo.so", ModuleAssemblyLoadContext.PlatformKind.Unix)
            .ToArray();
        await Assert.That(names).IsEquivalentTo(["libfoo.so"]);
    }

    [Test]
    [Arguments("win-x64", "Windows")]
    [Arguments("win", "Windows")]
    [Arguments("osx-arm64", "Apple")]
    [Arguments("maccatalyst-x64", "Apple")]
    [Arguments("ios-arm64", "Apple")]
    [Arguments("tvos-arm64", "Apple")]
    [Arguments("linux-x64", "Unix")]
    [Arguments("linux-musl-x64", "Unix")]
    [Arguments("linux-bionic-arm64", "Unix")]
    [Arguments("freebsd-x64", "Unix")]
    [Arguments("", "Unix")]
    public async Task ClassifyPlatform_MapsRidToPlatform(string rid, string expected)
    {
        var actual = ModuleAssemblyLoadContext.ClassifyPlatform(rid).ToString();
        await Assert.That(actual).IsEqualTo(expected);
    }
}
