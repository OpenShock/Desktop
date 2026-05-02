using System.IO;
using TUnit.Core;

namespace OpenShock.Desktop.ModuleManager.Tests;

public class NativeResolutionTests
{
    [Test]
    public async Task LinuxX64_FindsLibSoUnderRidFolder()
    {
        var module = FixtureLayout.CreateTempModule([
            "linux-x64/libSystem.IO.Ports.Native.so",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-x64");
        var resolved = alc.TryResolveNativeLibraryPath("System.IO.Ports.Native");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(module, "libs", "linux-x64", "libSystem.IO.Ports.Native.so"));
    }

    [Test]
    public async Task LinuxMuslX64_DoesNotFallBackToGlibc()
    {
        // Only the glibc variant is staged. A musl runtime must NOT pick it up — that .so
        // would dlopen with the wrong libc and crash. Loader returns null, runtime fails
        // loudly instead of silently misbehaving.
        var module = FixtureLayout.CreateTempModule([
            "linux-x64/libSystem.IO.Ports.Native.so",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-musl-x64");
        var resolved = alc.TryResolveNativeLibraryPath("System.IO.Ports.Native");

        await Assert.That(resolved).IsNull();
    }

    [Test]
    public async Task LinuxMuslX64_FindsItsOwnVariant_WhenStaged()
    {
        var module = FixtureLayout.CreateTempModule([
            "linux-x64/libSystem.IO.Ports.Native.so",
            "linux-musl-x64/libSystem.IO.Ports.Native.so",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-musl-x64");
        var resolved = alc.TryResolveNativeLibraryPath("System.IO.Ports.Native");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(module, "libs", "linux-musl-x64", "libSystem.IO.Ports.Native.so"));
    }

    [Test]
    public async Task NativeUnderRidNativeSubfolder_IsAlsoProbed()
    {
        // NuGet packages occasionally use runtimes/<rid>/native/ rather than runtimes/<rid>/.
        var module = FixtureLayout.CreateTempModule([
            "linux-x64/native/libSystem.IO.Ports.Native.so",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-x64");
        var resolved = alc.TryResolveNativeLibraryPath("System.IO.Ports.Native");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(module, "libs", "linux-x64", "native", "libSystem.IO.Ports.Native.so"));
    }

    [Test]
    public async Task OsxArm64_FindsDylibUnderRidFolder()
    {
        var module = FixtureLayout.CreateTempModule([
            "osx-arm64/libSystem.IO.Ports.Native.dylib",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "osx-arm64");
        var resolved = alc.TryResolveNativeLibraryPath("System.IO.Ports.Native");

        await Assert.That(resolved).IsEqualTo(
            Path.Combine(module, "libs", "osx-arm64", "libSystem.IO.Ports.Native.dylib"));
    }

    [Test]
    public async Task LinuxBionicArm64_DoesNotFallBackToGlibc()
    {
        // Bionic (Android) is a third libc. A glibc .so is not safe to dlopen on bionic;
        // the loader must refuse the cross-libc fallback exactly as it does for musl.
        var module = FixtureLayout.CreateTempModule([
            "linux-arm64/libfoo.so",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-bionic-arm64");
        var resolved = alc.TryResolveNativeLibraryPath("foo");

        await Assert.That(resolved).IsNull();
    }

    [Test]
    public async Task NativeMissingFromAnyRid_ReturnsNull()
    {
        var module = FixtureLayout.CreateTempModule([
            "linux/libSystem.IO.Ports.Native.so",
            "unix/libSystem.IO.Ports.Native.so",
        ]);

        var alc = new ModuleAssemblyLoadContext(module, "linux-x64");
        var resolved = alc.TryResolveNativeLibraryPath("System.IO.Ports.Native");

        // Native chain only contains the specific RID — it must NOT fall through to
        // linux/unix parent folders for native code.
        await Assert.That(resolved).IsNull();
    }
}
