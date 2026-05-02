using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace OpenShock.Desktop.ModuleManager.Tests;

internal static class FixtureLayout
{
    private static readonly ConcurrentBag<string> CreatedRoots = new();

    static FixtureLayout()
    {
        // Best-effort cleanup at process exit. TUnit runs the test exe in-process,
        // so this fires once after all tests finish — keeps %TEMP% from accumulating
        // openshock-loader-tests-* dirs across local test runs.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupAll();
    }

    public static string CreateTempModule(IEnumerable<string> relativeLibFiles)
    {
        var moduleRoot = Path.Combine(Path.GetTempPath(),
            "openshock-loader-tests-" + Path.GetRandomFileName());
        var libsRoot = Path.Combine(moduleRoot, "libs");
        Directory.CreateDirectory(libsRoot);

        foreach (var rel in relativeLibFiles)
        {
            var full = Path.Combine(libsRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, []);
        }

        CreatedRoots.Add(moduleRoot);
        return moduleRoot;
    }

    private static void CleanupAll()
    {
        foreach (var root in CreatedRoots)
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
