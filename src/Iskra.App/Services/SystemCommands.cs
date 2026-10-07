using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Iskra.Core.Search;

namespace Iskra.App.Services;

/// <summary>Системные команды без cmd/regedit: питание через WinAPI/ярлыки shell, корзина через SHEmptyRecycleBin.</summary>
internal static class SystemCommands
{
    public static Action<BuiltinItems.Cmd>? IskraHandler;

    public static void Run(BuiltinItems.Cmd c)
    {
        switch (c)
        {
            case BuiltinItems.Cmd.Lock: LockWorkStation(); break;
            case BuiltinItems.Cmd.Sleep: SetSuspendState(false, false, false); break;
            case BuiltinItems.Cmd.Hibernate: SetSuspendState(true, false, false); break;
            case BuiltinItems.Cmd.Restart: Shell("shutdown", "/r /t 0"); break;
            case BuiltinItems.Cmd.Shutdown: Shell("shutdown", "/s /t 0"); break;
            case BuiltinItems.Cmd.SignOut: ExitWindowsEx(0, 0); break;
            case BuiltinItems.Cmd.EmptyBin: SHEmptyRecycleBin(IntPtr.Zero, null, 0); break;
            case BuiltinItems.Cmd.OpenBin: OpenShell("shell:RecycleBinFolder"); break;
            case BuiltinItems.Cmd.RestartExplorer: RestartExplorer(); break;
            case BuiltinItems.Cmd.Screenshot: SendSnip(); break;
            default: IskraHandler?.Invoke(c); break;
        }
    }

    private static void Shell(string file, string args) => Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
    private static void OpenShell(string s) => Process.Start(new ProcessStartInfo("explorer.exe", s) { UseShellExecute = true });

    private static void RestartExplorer()
    {
        foreach (var p in Process.GetProcessesByName("explorer")) { try { p.Kill(); } catch { } }
        // explorer.exe сам перезапустится; подстрахуемся.
        try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); } catch { }
    }

    private static void SendSnip()
    {
        try { Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); }
        catch { try { Process.Start(new ProcessStartInfo("explorer.exe", "ms-screenclip:") { UseShellExecute = true }); } catch { } }
    }

    [DllImport("user32.dll")] private static extern bool LockWorkStation();
    [DllImport("powrprof.dll", SetLastError = true)] private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
    [DllImport("user32.dll")] private static extern bool ExitWindowsEx(uint flags, uint reason);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);
}
