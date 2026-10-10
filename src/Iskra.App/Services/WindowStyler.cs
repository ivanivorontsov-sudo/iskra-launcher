using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Iskra.Core.Themes;
using CW = Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>Материал/углы/тёмный режим окон самой Искры через DWM.</summary>
internal static class WindowStyler
{
    public static void Apply(Window window, ThemeDefinition theme, bool reduceEffects)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        ApplyToHwnd(hwnd, theme, reduceEffects);
    }

    /// <param name="foreign">Чужое окно (Проводник, диалоги): только рамка/углы/тёмный заголовок. Материал и
    /// расширение рамки на клиентскую область НЕ применяются — иначе содержимое Проводника становится полупрозрачным
    /// и на светлых темах (Прожектор) текст «засветляется».</param>
    public static void ApplyToHwnd(IntPtr hwnd, ThemeDefinition theme, bool reduceEffects, bool foreign = false)
    {
        // Для чужих окон тёмный заголовок синхронизируем с режимом приложений Windows, а не с темой лаунчера.
        int dark = (foreign ? theme.System.AppsDark : theme.Dark) ? 1 : 0;
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        int corner = theme.System.Corners switch
        {
            CornerStyle.Square => Native.DWMWCP_DONOTROUND,
            CornerStyle.RoundSmall => Native.DWMWCP_ROUNDSMALL,
            CornerStyle.Round => Native.DWMWCP_ROUND,
            _ => Native.DWMWCP_DEFAULT,
        };
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        bool backdropAvailable = !foreign && BackdropCaps.Available(reduceEffects);
        int backdrop = !backdropAvailable ? Native.DWMSBT_NONE : theme.Effects.Material switch
        {
            Material.Mica => Native.DWMSBT_MAINWINDOW,
            Material.MicaAlt => Native.DWMSBT_TABBED,
            Material.Acrylic => Native.DWMSBT_TRANSIENT,
            _ => Native.DWMSBT_NONE,
        };
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

        if (foreign)
        {
            // Снимаем то, что могла оставить 1.1.0 (стекло на всю клиентскую область Проводника).
            var zero = new Native.MARGINS();
            Native.DwmExtendFrameIntoClientArea(hwnd, ref zero);
        }
        else if (backdrop != Native.DWMSBT_NONE)
        {
            var m = new Native.MARGINS { L = -1, R = -1, T = -1, B = -1 };
            Native.DwmExtendFrameIntoClientArea(hwnd, ref m);
        }

        if (!string.IsNullOrEmpty(theme.Palette.Border))
        {
            var b = CW.Argb.Parse(theme.Palette.Border);
            uint border = b.ToColorRef();
            if (b.A > 20) Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_BORDER_COLOR, ref border, sizeof(uint));
        }
    }

    public static Color ToColor(string hex, Color fb) { var a = CW.Argb.Parse(hex, new CW.Argb(fb.A, fb.R, fb.G, fb.B)); return Color.FromArgb(a.A, a.R, a.G, a.B); }
}
