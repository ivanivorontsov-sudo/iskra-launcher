using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Iskra.Core.Themes;
using CW = Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>
/// Применяет материал/углы/тёмную рамку к окнам ДРУГИХ программ (Проводник и т. п.) через DWM-атрибуты —
/// как это делает Mica For Everyone. Не внедряет код в чужие процессы и не трогает системные DLL.
/// Периодически обходит видимые окна верхнего уровня.
/// </summary>
internal sealed class DwmWatcher
{
    public static DwmWatcher Instance { get; } = new();

    private readonly DispatcherTimer _timer;
    private ThemeDefinition? _theme;
    private bool _enabled;
    private readonly HashSet<IntPtr> _styled = new();

    // Классы окон, которые безопасно оформлять. Панель задач/«Пуск»/рабочий стол исключены.
    private static readonly string[] AllowedClasses = { "CabinetWClass", "ExploreWClass", "#32770" };
    private static readonly string[] SkipProcesses = { "explorer", "ApplicationFrameHost", "SystemSettings", "StartMenuExperienceHost", "SearchHost", "ShellExperienceHost", "TextInputHost" };

    private DwmWatcher()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _timer.Tick += (_, _) => Sweep();
    }

    public void Configure(ThemeDefinition theme, bool enabled)
    {
        _theme = theme;
        _enabled = enabled;
        _styled.Clear();
        if (enabled) { Sweep(); if (!_timer.IsEnabled) _timer.Start(); }
        else Stop();
    }

    public void Stop() => _timer.Stop();

    private void Sweep()
    {
        if (!_enabled || _theme == null) return;
        try { EnumWindows(Callback, IntPtr.Zero); } catch (Exception ex) { Log.Error("DWM-обход окон", ex); }
    }

    private bool Callback(IntPtr hwnd, IntPtr lparam)
    {
        if (!IsWindowVisible(hwnd)) return true;
        var cls = GetClass(hwnd);
        // Проводник (CabinetWClass) — главный кандидат. Диалоги #32770 оформляем тоже.
        if (Array.IndexOf(AllowedClasses, cls) < 0) return true;
        if (_styled.Contains(hwnd)) return true;
        try
        {
            WindowStyler.ApplyToHwnd(hwnd, _theme!, reduceEffects: false, foreign: true);
            _styled.Add(hwnd);
        }
        catch { }
        return true;
    }

    private static string GetClass(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        Native.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
}
