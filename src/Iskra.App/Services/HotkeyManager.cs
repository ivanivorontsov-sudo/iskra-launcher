using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Iskra.Core.Settings;

namespace Iskra.App.Services;

/// <summary>Глобальные горячие клавиши через RegisterHotKey + HwndSource. Сообщает о конфликте, если регистрация не удалась.</summary>
internal sealed class HotkeyManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 0xB000;

    public HotkeyManager(Window messageWindow)
    {
        _hwnd = new WindowInteropHelper(messageWindow).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd)!;
        _source.AddHook(WndProc);
    }

    public (bool ok, int id) TryRegister(Hotkey hk, Action action)
    {
        if (hk.IsEmpty) return (false, 0);
        uint mods = 0;
        if (hk.Modifiers.HasFlag(HotkeyModifiers.Alt)) mods |= Native.MOD_ALT;
        if (hk.Modifiers.HasFlag(HotkeyModifiers.Ctrl)) mods |= Native.MOD_CONTROL;
        if (hk.Modifiers.HasFlag(HotkeyModifiers.Shift)) mods |= Native.MOD_SHIFT;
        if (hk.Modifiers.HasFlag(HotkeyModifiers.Win)) mods |= Native.MOD_WIN;
        mods |= Native.MOD_NOREPEAT;
        uint vk = ToVk(hk.Key);
        if (vk == 0) return (false, 0);
        int id = _nextId++;
        if (!Native.RegisterHotKey(_hwnd, id, mods, vk)) return (false, 0);
        _actions[id] = action;
        return (true, id);
    }

    public void Unregister(int id)
    {
        if (_actions.Remove(id)) Native.UnregisterHotKey(_hwnd, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in new List<int>(_actions.Keys)) Native.UnregisterHotKey(_hwnd, id);
        _actions.Clear();
    }

    /// <summary>Проверка занятости: пробуем зарегистрировать и сразу снимаем.</summary>
    public bool IsFree(Hotkey hk)
    {
        var (ok, id) = TryRegister(hk, () => { });
        if (ok) Unregister(id);
        return ok;
    }

    private static uint ToVk(string key)
    {
        var k = (Key)Enum.Parse(typeof(Key), key, true);
        return (uint)KeyInterop.VirtualKeyFromKey(k);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && _actions.TryGetValue(w.ToInt32(), out var a))
        {
            handled = true;
            try { a(); } catch (Exception ex) { Log.Error("Горячая клавиша", ex); }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
    }
}
