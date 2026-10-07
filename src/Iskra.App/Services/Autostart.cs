using System;
using System.IO;
using Microsoft.Win32;

namespace Iskra.App.Services;

/// <summary>Автозапуск через HKCU\...\Run (.NET Registry API, без reg.exe). Разрешён охранником реестра? Нет — это Run.
/// Поэтому используем ярлык в папке «Автозагрузка» как основной способ и HKCU Run как запасной (оба не требуют прав администратора).</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Iskra";

    public static string StartupShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Искра.lnk");

    public static bool IsEnabled()
    {
        if (File.Exists(StartupShortcut)) return true;
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(ValueName) != null;
    }

    public static void Set(bool enabled, string exePath)
    {
        try
        {
            if (enabled)
            {
                ShellLink.Create(StartupShortcut, exePath, "--tray", Path.GetDirectoryName(exePath)!, "Искра — лаунчер и оформление Windows");
            }
            else
            {
                if (File.Exists(StartupShortcut)) File.Delete(StartupShortcut);
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
                k?.DeleteValue(ValueName, false);
            }
        }
        catch (Exception ex) { Log.Error("Автозапуск", ex); }
    }
}
