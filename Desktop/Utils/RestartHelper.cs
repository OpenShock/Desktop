using System.Diagnostics;

namespace OpenShock.Desktop.Utils;

/// <summary>
/// Restarts the running application. The mechanism is platform specific, but callers
/// just invoke <see cref="RestartApp"/> regardless of platform.
/// </summary>
public static class RestartHelper
{
#if WINDOWS
    private static readonly string PowerShellLogFile = Path.Combine(Constants.LogsFolder, "restart.log");
#endif

    public static void RestartApp()
    {
#if WINDOWS
        var currentExePath = Environment.ProcessPath;
        var processId = Environment.ProcessId;

        // PowerShell helper: wait for this process to exit (up to 30s), then relaunch.
        var scriptContent = $$"""
            Start-Transcript -Path '{{PowerShellLogFile}}'
            $processID = {{processId}}
            $timeout = New-TimeSpan -Seconds 30
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            Write-Host "Waiting for process with ID $processID to stop running..."
            do {
                $process = Get-Process -Id $processID -ErrorAction SilentlyContinue
                if ($process -ne $null) {
                    Write-Host "Still waiting for process with ID $processID to stop running... $sw.Elapsed"
                    Start-Sleep -Seconds 1
                }
            } while ($process -ne $null -and $sw.Elapsed -lt $timeout)

            if ($process -eq $null) {
                $exePath = '{{currentExePath}}'
                Write-Host "Starting $exePath after $sw.Elapsed"
                Start-Process -FilePath $exePath
                Start-Sleep -Seconds 2
            }
            else {
                Write-Host "Process with ID $processID is still running. Timed out after $($timeout.TotalSeconds) seconds."
                Write-Host "Please make sure the process with ID $processID is not running before starting a new instance."
                Start-Sleep -Seconds 10
            }
            Stop-Transcript
            """;

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-Command \"{scriptContent}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        Process.Start(startInfo);

        if (Application.Current != null)
        {
            Application.Current.Quit();
            return;
        }

        Environment.Exit(0);
#elif PHOTINO
        var pid = Environment.ProcessId;

        // When running from an AppImage, Environment.ProcessPath points inside the
        // FUSE mount (e.g. /tmp/.mount_XXXX/usr/bin/OpenShock.Desktop), which is
        // unmounted as soon as we exit. The AppImage runtime exposes the real path
        // of the .AppImage file via $APPIMAGE, so relaunch that instead.
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        var target = !string.IsNullOrEmpty(appImage) ? appImage : Environment.ProcessPath;

        if (string.IsNullOrEmpty(target))
        {
            Environment.Exit(0);
            return;
        }

        // Detached helper: wait for this process to fully exit (up to ~30s), then
        // relaunch the target. Passed as positional args so paths with spaces are safe:
        //   $1 = target executable, $2 = pid to wait on
        const string script =
            """
            target="$1"
            pid="$2"
            i=0
            while kill -0 "$pid" 2>/dev/null; do
                sleep 0.5
                i=$((i + 1))
                [ "$i" -ge 60 ] && break
            done
            exec "$target"
            """;

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
        };

        // setsid detaches the helper into its own session so it survives our exit and
        // does not inherit a controlling terminal. Fall back to a plain shell if it is
        // somehow unavailable (setsid ships with util-linux and is present on all
        // mainstream distros, but be defensive).
        try
        {
            startInfo.FileName = "setsid";
            startInfo.ArgumentList.Add("/bin/sh");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(script);
            startInfo.ArgumentList.Add("sh"); // $0
            startInfo.ArgumentList.Add(target); // $1
            startInfo.ArgumentList.Add(pid.ToString()); // $2
            Process.Start(startInfo);
        }
        catch
        {
            startInfo.FileName = "/bin/sh";
            startInfo.ArgumentList.Clear();
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(script);
            startInfo.ArgumentList.Add("sh"); // $0
            startInfo.ArgumentList.Add(target); // $1
            startInfo.ArgumentList.Add(pid.ToString()); // $2
            Process.Start(startInfo);
        }

        Environment.Exit(0);
#else
        // No restart mechanism for other targets (e.g. the web host); no-op.
#endif
    }
}
