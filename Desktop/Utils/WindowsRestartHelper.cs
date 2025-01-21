#if WINDOWS
using System.Diagnostics;

namespace OpenShock.Desktop.Utils;

public static class WindowsRestartHelper
{
    private static readonly string PowerShellLogFile = Path.Combine(Constants.LogsFolder, "restart.log");
    
    public static void RestartApp()
    {
        
        var currentExePath = Environment.ProcessPath;
        var processId = Environment.ProcessId;
        
        // Create the PowerShell script as a string
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

        // Execute the PowerShell script
        var startInfo = new ProcessStartInfo {
            FileName = "powershell.exe",
            Arguments = $"-Command \"{scriptContent}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        
        var process = new Process
        {
            StartInfo = startInfo
        };
        process.Start();
        
        if (Application.Current != null)
        {
            Application.Current.Quit();
            return;
        }
        
        Environment.Exit(0);

        
    } 
}
#endif