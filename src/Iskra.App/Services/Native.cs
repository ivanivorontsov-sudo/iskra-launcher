using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Iskra.App.Services;

/// <summary>P/Invoke в один файл: DWM-материалы, глобальные горячие клавиши, клавиатурный хук, окно и питание.</summary>
internal static class Native
{
    // ---- DWM ----
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_BORDER_COLOR = 34;
    public const int DWMWA_CAPTION_COLOR = 35;
    public const int DWMWA_TEXT_COLOR = 36;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public const int DWMSBT_AUTO = 0, DWMSBT_NONE = 1, DWMSBT_MAINWINDOW = 2, DWMSBT_TRANSIENT = 3, DWMSBT_TABBED = 4;
    public const int DWMWCP_DEFAULT = 0, DWMWCP_DONOTROUND = 1, DWMWCP_ROUND = 2, DWMWCP_ROUNDSMALL = 3;
    public const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF, DWMWA_COLOR_NONE = 0xFFFFFFFE;

    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);

    [StructLayout(LayoutKind.Sequential)] public struct MARGINS { public int L, R, T, B; }

    // ---- Window composition (Acrylic на Win10 / классических окнах) ----
    [StructLayout(LayoutKind.Sequential)]
    public struct ACCENT_POLICY { public int AccentState; public int AccentFlags; public uint GradientColor; public int AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWCOMPOSITIONATTRIBDATA { public int Attribute; public IntPtr Data; public int SizeOfData; }
    public const int ACCENT_DISABLED = 0, ACCENT_ENABLE_BLURBEHIND = 3, ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;
    public const int WCA_ACCENT_POLICY = 19;
    [DllImport("user32.dll")] public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

    // ---- Горячие клавиши ----
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    // ---- Низкоуровневый клавиатурный хук (перехват Win) ----
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
    public const int VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_DUMMY = 0xFF;
    public const uint KEYEVENTF_KEYUP = 0x2;
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc proc, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string? name);

    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int cb);
    public static readonly IntPtr IskraMarker = new(0x15C4A);

    // ---- Окна ----
    public const int GWL_EXSTYLE = -20, GWL_STYLE = -16;
    public const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000, WS_EX_APPWINDOW = 0x40000;
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hwnd, int index, int val);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? cls, string? win);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr child, string? cls, string? win);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);

    // ---- Рассылка смены темы ----
    public const int WM_SETTINGCHANGE = 0x1A;
    public const uint SMTO_ABORTIFHUNG = 2;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr w, string l, uint flags, uint timeout, out IntPtr res);

    // ---- SystemParametersInfo: обои, курсоры, интервал значков ----
    public const uint SPI_SETDESKWALLPAPER = 0x14, SPI_SETCURSORS = 0x57, SPIF_UPDATEINIFILE = 0x1, SPIF_SENDCHANGE = 0x2;
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SystemParametersInfo(uint action, uint uParam, string? pv, uint winIni);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)] public static extern bool SystemParametersInfoPtr(uint action, uint uParam, IntPtr pv, uint winIni);

    // ---- Панель задач: автоскрытие ----
    [StructLayout(LayoutKind.Sequential)] public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }
    public const uint ABM_GETSTATE = 4, ABM_SETSTATE = 10, ABS_AUTOHIDE = 1, ABS_ALWAYSONTOP = 2;
    [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);

    [DllImport("user32.dll")] public static extern short VkKeyScan(char ch);

    // ---- Обновление кэша значков ----
    public const uint SHCNE_UPDATEITEM = 0x00002000;
    public const uint SHCNE_ASSOCCHANGED = 0x08000000;
    public const uint SHCNF_IDLIST = 0x0;
    public const uint SHCNF_PATHW = 0x0005;
    [DllImport("shell32.dll")] public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern void SHChangeNotify(uint eventId, uint flags, string item1, IntPtr item2);

    // ---- Шрифты: регистрация, рассылка, живое применение (SPI) ----
    public const int WM_FONTCHANGE = 0x001D;
    public const uint SPI_GETNONCLIENTMETRICS = 0x0029, SPI_SETNONCLIENTMETRICS = 0x002A;
    public const uint SPI_GETICONMETRICS = 0x002D, SPI_SETICONMETRICS = 0x002E;
    public const int LF_FACESIZE = 32;

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern int AddFontResource(string path);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern int RemoveFontResource(string path);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct LOGFONT
    {
        public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
        public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = LF_FACESIZE)] public string lfFaceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NONCLIENTMETRICS
    {
        public int cbSize;
        public int iBorderWidth, iScrollWidth, iScrollHeight, iCaptionWidth, iCaptionHeight;
        public LOGFONT lfCaptionFont;
        public int iSmCaptionWidth, iSmCaptionHeight;
        public LOGFONT lfSmCaptionFont;
        public int iMenuWidth, iMenuHeight;
        public LOGFONT lfMenuFont, lfStatusFont, lfMessageFont;
        public int iPaddedBorderWidth; // Vista+
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ICONMETRICS
    {
        public int cbSize, iHorzSpacing, iVertSpacing, iTitleWrap;
        public LOGFONT lfFont;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uParam, ref NONCLIENTMETRICS pv, uint winIni);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uParam, ref ICONMETRICS pv, uint winIni);
}
