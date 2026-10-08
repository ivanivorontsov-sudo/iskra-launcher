using System;
using System.Runtime.InteropServices;

namespace Iskra.App.Services;

/// <summary>
/// Безопасный режим: минимальная тема, непрозрачный фон, программная отрисовка, без применения темы к Windows.
/// Включается, если удерживать Shift при запуске или если прошлый сеанс завершился аварийно.
/// </summary>
internal static class SafeMode
{
    public static bool Active { get; private set; }
    public static string Reason { get; private set; } = "";

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    private const int VK_SHIFT = 0x10;

    public static void Evaluate()
    {
        bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        if (shift) { Active = true; Reason = "удержан Shift"; }
        else if (RunState.CrashedLastTime) { Active = true; Reason = "прошлый сеанс завершился аварийно"; }
        if (Active) Log.Warn($"Безопасный режим: {Reason}");
    }

    public static void ForceEnable(string reason) { Active = true; Reason = reason; }
}
