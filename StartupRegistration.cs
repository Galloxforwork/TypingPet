using Microsoft.Win32;
using System.Windows.Forms;

namespace TypingPet;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TypingPet";

    public static string? Synchronize(bool enabled)
    {
#if DEBUG
        // A developer run must not replace the user's release EXE in Windows startup.
        return null;
#else
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return "无法访问当前用户的开机启动设置。";
            if (enabled)
                key.SetValue(ValueName, $"\"{Application.ExecutablePath}\" --background", RegistryValueKind.String);
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
#endif
    }
}
