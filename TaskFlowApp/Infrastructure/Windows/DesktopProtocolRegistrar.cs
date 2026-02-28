#if WINDOWS
using Microsoft.Win32;

namespace TaskFlowApp.Infrastructure.Windows;

internal static class DesktopProtocolRegistrar
{
    private const string ProtocolName = "taskflowapp";
    private const string ProtocolDisplayName = "URL:TaskFlowApp Protocol";

    public static void EnsureRegistered()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return;
            }

            using var protocolKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}");
            if (protocolKey is null)
            {
                return;
            }

            protocolKey.SetValue(string.Empty, ProtocolDisplayName);
            protocolKey.SetValue("URL Protocol", string.Empty);

            using var iconKey = protocolKey.CreateSubKey("DefaultIcon");
            iconKey?.SetValue(string.Empty, $"\"{executablePath}\",0");

            using var commandKey = protocolKey.CreateSubKey(@"shell\open\command");
            commandKey?.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
        }
        catch
        {
            // Protocol registration should never block app startup.
        }
    }
}
#endif
