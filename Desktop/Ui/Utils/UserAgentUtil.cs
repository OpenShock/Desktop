using System.Runtime.InteropServices;
using OpenShock.SDK.CSharp.Utils;

namespace OpenShock.Desktop.Ui.Utils;

public static class UserAgentUtil
{
    public static string GetUserAgent()
    {
        var desktopVersion = typeof(UserAgentUtils).Assembly.GetName().Version!;
        
        var runtimeVersion = RuntimeInformation.FrameworkDescription;
        if (string.IsNullOrEmpty(runtimeVersion)) runtimeVersion = "Unknown Runtime";

        return
            $"OpenShock.Desktop/{desktopVersion.Major}.{desktopVersion.Minor}.{desktopVersion.Build} " +
            $"({runtimeVersion}; {UserAgentUtils.GetOs()}; OpenShockDesktopInternals)";
    }
}