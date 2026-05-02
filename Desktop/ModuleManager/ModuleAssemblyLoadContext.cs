using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace OpenShock.Desktop.ModuleManager;

public class ModuleAssemblyLoadContext : AssemblyLoadContext
{
    internal enum PlatformKind { Windows, Apple, Unix }

    private readonly string _moduleLibPath;
    private readonly IReadOnlyList<string> _managedRidChain;
    private readonly IReadOnlyList<string> _nativeRidChain;
    private readonly PlatformKind _platform;

    public ModuleAssemblyLoadContext(string modulePath)
        : this(modulePath, DetectRuntimeRid())
    {
    }

    internal ModuleAssemblyLoadContext(string modulePath, string runtimeRid)
    {
        _moduleLibPath = Path.Combine(modulePath, "libs");
        _managedRidChain = BuildManagedRidChain(runtimeRid);
        _nativeRidChain = BuildNativeRidChain(runtimeRid);
        _platform = ClassifyPlatform(runtimeRid);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = TryResolveManagedAssemblyPath(assemblyName.Name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = TryResolveNativeLibraryPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    internal string? TryResolveManagedAssemblyPath(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName)) return null;

        var fileName = assemblyName + ".dll";

        foreach (var rid in _managedRidChain)
        {
            var path = rid.Length == 0
                ? Path.Combine(_moduleLibPath, fileName)
                : Path.Combine(_moduleLibPath, rid, fileName);

            if (File.Exists(path)) return path;
        }

        return null;
    }

    internal string? TryResolveNativeLibraryPath(string unmanagedDllName)
    {
        foreach (var rid in _nativeRidChain)
        {
            var ridDir = Path.Combine(_moduleLibPath, rid, "native");
            var flatRidDir = Path.Combine(_moduleLibPath, rid);

            foreach (var candidate in EnumerateNativeFileNames(unmanagedDllName, _platform))
            {
                var nativePath = Path.Combine(ridDir, candidate);
                if (File.Exists(nativePath)) return nativePath;

                var flatPath = Path.Combine(flatRidDir, candidate);
                if (File.Exists(flatPath)) return flatPath;
            }
        }

        return null;
    }

    internal static IEnumerable<string> EnumerateNativeFileNames(string name, PlatformKind platform)
    {
        yield return name;

        if (platform == PlatformKind.Windows)
        {
            if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                yield return name + ".dll";
            yield break;
        }

        var hasLibPrefix = name.StartsWith("lib", StringComparison.Ordinal);

        if (platform == PlatformKind.Apple)
        {
            var hasExt = name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase);
            if (!hasExt) yield return name + ".dylib";
            if (!hasLibPrefix)
            {
                yield return "lib" + name;
                if (!hasExt) yield return "lib" + name + ".dylib";
            }
            yield break;
        }

        // linux, android (bionic), freebsd, illumos, solaris
        var hasSoExt = name.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains(".so.", StringComparison.OrdinalIgnoreCase);

        if (!hasSoExt) yield return name + ".so";
        if (!hasLibPrefix)
        {
            yield return "lib" + name;
            if (!hasSoExt) yield return "lib" + name + ".so";
        }
    }

    internal static PlatformKind ClassifyPlatform(string rid)
    {
        var (os, _) = SplitRid(rid);
        return os switch
        {
            "win" => PlatformKind.Windows,
            "osx" or "maccatalyst" or "ios" or "iossimulator" or "tvos" or "tvossimulator" => PlatformKind.Apple,
            _ => PlatformKind.Unix,
        };
    }

    internal static IReadOnlyList<string> BuildManagedRidChain(string currentRid)
    {
        var chain = new List<string>();

        void Add(string rid)
        {
            if (rid.Length == 0 || !chain.Contains(rid, StringComparer.Ordinal)) chain.Add(rid);
        }

        Add(currentRid);

        var (os, arch) = SplitRid(currentRid);
        var hasArch = arch.Length > 0;

        switch (os)
        {
            case "win":
                if (hasArch) Add($"win-{arch}");
                Add("win");
                break;

            case "osx":
                if (hasArch) Add($"osx-{arch}");
                Add("osx");
                if (hasArch) Add($"unix-{arch}");
                Add("unix");
                break;

            case "maccatalyst":
                if (hasArch) Add($"maccatalyst-{arch}");
                Add("maccatalyst");
                if (hasArch) Add($"osx-{arch}");
                Add("osx");
                if (hasArch) Add($"unix-{arch}");
                Add("unix");
                break;

            case "linux-musl":
                if (hasArch) Add($"linux-musl-{arch}");
                Add("linux-musl");
                if (hasArch) Add($"linux-{arch}");
                Add("linux");
                if (hasArch) Add($"unix-{arch}");
                Add("unix");
                break;

            case "linux-bionic":
                if (hasArch) Add($"linux-bionic-{arch}");
                Add("linux-bionic");
                if (hasArch) Add($"linux-{arch}");
                Add("linux");
                if (hasArch) Add($"unix-{arch}");
                Add("unix");
                break;

            case "linux":
                if (hasArch) Add($"linux-{arch}");
                Add("linux");
                if (hasArch) Add($"unix-{arch}");
                Add("unix");
                break;

            case "freebsd":
                if (hasArch) Add($"freebsd-{arch}");
                Add("freebsd");
                if (hasArch) Add($"unix-{arch}");
                Add("unix");
                break;
        }

        Add(string.Empty);
        return chain;
    }

    internal static IReadOnlyList<string> BuildNativeRidChain(string currentRid)
    {
        // Native code only lives under specific-arch RID folders. No `unix`/`linux`/`win`
        // parent-folder fallback: those never contain native binaries, and cross-libc
        // fallback (musl -> glibc) would try to load an ABI-incompatible .so.
        return [currentRid];
    }

    internal static (string os, string arch) SplitRid(string rid)
    {
        // RID shape: <os>[-<flavor>]-<arch>  e.g. win-x64, linux-x64, linux-musl-x64,
        // linux-bionic-arm64, osx-arm64, maccatalyst-x64, freebsd-x64.
        ReadOnlySpan<string> knownArches =
        [
            "x64", "x86", "arm64", "arm", "armel",
            "wasm", "loongarch64", "ppc64le", "riscv64", "s390x", "mips64"
        ];

        foreach (var a in knownArches)
        {
            if (rid.Length > a.Length + 1 &&
                rid.EndsWith(a, StringComparison.Ordinal) &&
                rid[^(a.Length + 1)] == '-')
            {
                return (rid[..^(a.Length + 1)], a);
            }
        }

        return (rid, string.Empty);
    }

    internal static string DetectRuntimeRid()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;

        // RuntimeInformation.RuntimeIdentifier reflects the published RID, which on a
        // self-contained `dotnet publish -r linux-x64` deployment is "linux-x64" even when
        // executed on Alpine/musl. The published runtime + native shims will be glibc, so
        // this loader's per-module musl fallback is only useful when a bundled module
        // happens to ship a musl flavor — but if we ever do detect musl, prefer the
        // linux-musl chain so loads route to those folders first.
        if (!OperatingSystem.IsLinux() || OperatingSystem.IsAndroid()) return rid;
        if (!rid.StartsWith("linux-", StringComparison.Ordinal)) return rid;
        if (rid.StartsWith("linux-musl-", StringComparison.Ordinal)) return rid;
        if (rid.StartsWith("linux-bionic-", StringComparison.Ordinal)) return rid;

        return IsMuslLinux() ? "linux-musl-" + rid["linux-".Length..] : rid;
    }

    private static bool IsMuslLinux()
    {
        // Alpine / musl ship the dynamic linker as /lib/ld-musl-<arch>.so.1.
        // glibc systems ship /lib/ld-linux-* or /lib64/ld-linux-* — never ld-musl-*.
        return HasMuslLoader("/lib") || HasMuslLoader("/lib64");
    }

    private static bool HasMuslLoader(string dir)
    {
        try
        {
            return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "ld-musl-*").Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // I/O failure during detection — assume glibc.
            return false;
        }
    }
}