using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using TUnit.Core;
using TUnit.Core.Exceptions;

namespace OpenShock.Desktop.ModuleManager.Tests;

/// <summary>
/// End-to-end test: stage a real System.IO.Ports nuget into a fake module's libs/
/// tree (matching the OpenShock.LocalRelay layout), load it through the per-module
/// ALC, and invoke SerialPort.GetPortNames(). Catches regressions where managed
/// resolution succeeds but the native shim's dlopen/LoadLibrary fails.
/// </summary>
public class SerialPortIntegrationTests
{
    [Test]
    public async Task LoadAndCallGetPortNames_Succeeds()
    {
        if (!TryStageRealisticFixture(out var moduleRoot, out var skipReason))
        {
            // Test infrastructure isn't available (CI runner without nuget cache, unusual
            // package layout, unsupported host RID). Skip rather than fail — the unit
            // tests still cover the resolution logic.
            throw new SkipTestException(skipReason);
        }

        var alc = new ModuleAssemblyLoadContext(moduleRoot);
        var assembly = alc.LoadFromAssemblyName(new AssemblyName("System.IO.Ports"));

        var serialPortType = assembly.GetType("System.IO.Ports.SerialPort", throwOnError: true)!;
        var getPortNames = serialPortType.GetMethod(
            "GetPortNames",
            BindingFlags.Public | BindingFlags.Static,
            Type.EmptyTypes);

        await Assert.That(getPortNames).IsNotNull();

        // The call exercises the native shim on Linux/macOS. We don't assert on contents
        // (CI runners have no real ports). What matters is that it returns instead of
        // throwing DllNotFoundException for libSystem.IO.Ports.Native.
        var result = getPortNames!.Invoke(null, null);
        await Assert.That(result).IsTypeOf<string[]>();
    }

    private static bool TryStageRealisticFixture(out string moduleRoot, out string skipReason)
    {
        moduleRoot = string.Empty;
        skipReason = string.Empty;

        var packageRoot = ReadPackagePathFile();
        if (packageRoot is null || !Directory.Exists(packageRoot))
        {
            skipReason = $"System.IO.Ports nuget package root not found (looked at '{packageRoot}'). " +
                         "MSBuild target may not have run, or the package wasn't restored.";
            return false;
        }

        var winManaged = FindHighestTfmManagedAssembly(
            Path.Combine(packageRoot, "runtimes", "win", "lib"), "System.IO.Ports.dll");
        var unixManaged = FindHighestTfmManagedAssembly(
            Path.Combine(packageRoot, "runtimes", "unix", "lib"), "System.IO.Ports.dll");
        var rootFacade = FindHighestTfmManagedAssembly(
            Path.Combine(packageRoot, "lib"), "System.IO.Ports.dll");

        if (winManaged is null || unixManaged is null || rootFacade is null)
        {
            skipReason = "System.IO.Ports nuget didn't expose the expected runtimes/win, " +
                         "runtimes/unix, or lib folders. Package layout may have changed.";
            return false;
        }

        var rid = RuntimeInformation.RuntimeIdentifier;
        var nativeFile = OperatingSystem.IsWindows()
            ? null // win uses kernel32 comms APIs directly; no native shim shipped.
            : LocateNativeShim(packageRoot, rid);

        if (!OperatingSystem.IsWindows() && nativeFile is null)
        {
            skipReason = $"No native shim staged for RID '{rid}'. " +
                         "Package may not ship a binary for this runtime.";
            return false;
        }

        moduleRoot = Path.Combine(Path.GetTempPath(),
            "openshock-loader-integration-" + Path.GetRandomFileName());
        var libsRoot = Path.Combine(moduleRoot, "libs");
        Directory.CreateDirectory(libsRoot);

        // Mirror the OpenShock.LocalRelay bundle shape: parent-RID managed at libs/win/
        // and libs/unix/, AnyCPU facade at libs/, native shim under libs/<rid>/.
        Directory.CreateDirectory(Path.Combine(libsRoot, "win"));
        File.Copy(winManaged, Path.Combine(libsRoot, "win", "System.IO.Ports.dll"));

        Directory.CreateDirectory(Path.Combine(libsRoot, "unix"));
        File.Copy(unixManaged, Path.Combine(libsRoot, "unix", "System.IO.Ports.dll"));

        File.Copy(rootFacade, Path.Combine(libsRoot, "System.IO.Ports.dll"));

        if (nativeFile is not null)
        {
            var ridDir = Path.Combine(libsRoot, rid);
            Directory.CreateDirectory(ridDir);
            File.Copy(nativeFile, Path.Combine(ridDir, Path.GetFileName(nativeFile)));
        }

        return true;
    }

    private static string? ReadPackagePathFile()
    {
        var probe = Path.Combine(AppContext.BaseDirectory, "system_io_ports_package_path.txt");
        if (!File.Exists(probe)) return null;

        var contents = File.ReadAllText(probe).Trim();
        return string.IsNullOrEmpty(contents) ? null : contents;
    }

    private static string? FindHighestTfmManagedAssembly(string libRoot, string fileName)
    {
        if (!Directory.Exists(libRoot)) return null;

        // Pick the highest net*/netstandard* folder that has the file. Sort by parsed
        // (major, minor) for net*, with netstandard ranked below all net*. Lexicographic
        // ordering would put net10.0 BELOW net9.0 ('1' < '9' in ASCII).
        return Directory.EnumerateDirectories(libRoot)
            .Select(d => new { Dir = d, File = Path.Combine(d, fileName) })
            .Where(c => File.Exists(c.File))
            .OrderByDescending(c => TfmRank(Path.GetFileName(c.Dir)))
            .FirstOrDefault()?.File;
    }

    private static (int family, int major, int minor) TfmRank(string tfm)
    {
        // family: 1 = net*, 0 = netstandard*, -1 = other.
        if (tfm.StartsWith("net", StringComparison.OrdinalIgnoreCase) &&
            !tfm.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseVersion(tfm.AsSpan(3), out var maj, out var min)
                ? (1, maj, min)
                : (1, 0, 0);
        }

        if (tfm.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseVersion(tfm.AsSpan("netstandard".Length), out var maj, out var min)
                ? (0, maj, min)
                : (0, 0, 0);
        }

        return (-1, 0, 0);
    }

    private static bool TryParseVersion(ReadOnlySpan<char> s, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        var dot = s.IndexOf('.');
        if (dot < 0) return int.TryParse(s, out major);

        if (!int.TryParse(s[..dot], out major)) return false;

        var rest = s[(dot + 1)..];
        // Strip a trailing "-windows" / "-android" / etc. flavor on the minor part.
        var dash = rest.IndexOf('-');
        if (dash >= 0) rest = rest[..dash];
        return int.TryParse(rest, out minor);
    }

    private static string? LocateNativeShim(string packageRoot, string rid)
    {
        var ridRoot = Path.Combine(packageRoot, "runtimes", rid, "native");
        if (!Directory.Exists(ridRoot)) return null;

        // Linux uses .so, macOS uses .dylib. We don't filter — first match wins.
        return Directory.EnumerateFiles(ridRoot, "*System.IO.Ports.Native*").FirstOrDefault();
    }
}
