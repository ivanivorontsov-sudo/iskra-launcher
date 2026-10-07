using System;
using Iskra.App;

namespace Iskra.App.Services;

/// <summary>
/// Перехват клавиши Win для замены меню «Пуск». Низкоуровневый хук ловит отпускание Win и вместо открытия
/// системного «Пуска» вставляет «пустую» клавишу (0xFF), как это делает PowerToys, а затем вызывает наш обработчик.
/// Комбинации Win+<буква> (Win+E, Win+R…) не трогаем.
/// </summary>
internal sealed class StartKeyHook : IDisposable
{
    private readonly Action _onStart;
    private IntPtr _hook;
    private Native.LowLevelKeyboardProc _proc = null!;
    private bool _winDown;
    private bool _winCombo;
    public bool Enabled { get; set; } = true;

    public StartKeyHook(Action onStart) => _onStart = onStart;

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = HookProc;
        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) Log.Warn("Не удалось установить клавиатурный хук (замена «Пуска» недоступна)");
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Enabled)
        {
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            // Не реагируем на наши собственные синтетические нажатия.
            bool injectedByUs = data.dwExtraInfo == Native.IskraMarker;
            int msg = wParam.ToInt32();
            bool isWin = data.vkCode is Native.VK_LWIN or Native.VK_RWIN;
            if (!injectedByUs)
            {
                if (isWin && msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN) { _winDown = true; _winCombo = false; }
                else if (isWin && msg is Native.WM_KEYUP or Native.WM_SYSKEYUP)
                {
                    bool wasAlone = _winDown && !_winCombo;
                    _winDown = false;
                    if (wasAlone)
                    {
                        // Гасим системный «Пуск»: впрыскиваем пустую клавишу и открываем своё меню.
                        SendDummy();
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(_onStart);
                        return new IntPtr(1);
                    }
                }
                else if (_winDown && msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN)
                {
                    _winCombo = true; // Win+<что-то> — системное сочетание, пропускаем
                }
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static void SendDummy()
    {
        var inputs = new[]
        {
            new Native.INPUT { type = 1, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = Native.VK_DUMMY, dwExtraInfo = Native.IskraMarker } } },
            new Native.INPUT { type = 1, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = Native.VK_DUMMY, dwFlags = Native.KEYEVENTF_KEYUP, dwExtraInfo = Native.IskraMarker } } },
        };
        Native.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Native.INPUT>());
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }
}

