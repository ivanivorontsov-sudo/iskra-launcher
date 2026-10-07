using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Iskra.App.Services;
using Iskra.Core.Loc;

namespace Iskra.App;

internal static class Program
{
    private static Mutex? _mutex;
    public static AppState State { get; private set; } = null!;

    [STAThread]
    public static int Main(string[] args)
    {
        // Повышенные служебные режимы (вызываются самим приложением через UAC).
        if (args.Length >= 1)
        {
            switch (args[0])
            {
                case "--apply-system-font" when args.Length >= 3:
                    return SystemFontApplier.RunElevatedApply(args[1], args[2]);
                case "--revert-system-font":
                    return SystemFontApplier.RunElevatedRevert();
                case "--restore-lockscreen":
                    RestoreLockScreenFromBackup();
                    return 0;
                case "--restore-silent":
                    try { new SystemThemeApplier().Restore(includeHklm: AppInfo.IsAdmin()); } catch (Exception ex) { Log.Error("restore-silent", ex); }
                    return 0;
            }
        }

        bool created;
        _mutex = new Mutex(true, "Iskra_SingleInstance_8F2A", out created);
        if (!created)
        {
            // Уже запущена — просто выходим (можно расширить до межпроцессного «показать окно»).
            return 0;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Необработанное исключение", e.ExceptionObject as Exception);
        app.DispatcherUnhandledException += (_, e) => { Log.Error("Исключение UI", e.Exception); e.Handled = true; };

        // Скрытое окно-приёмник сообщений (для горячих клавиш и трея).
        var messageWindow = new Window
        {
            Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false,
            ShowActivated = false, Visibility = Visibility.Hidden, Left = -10000, Top = -10000,
        };
        messageWindow.Show();
        messageWindow.Hide();

        State = new AppState();
        State.Initialize(messageWindow);

        using var tray = new TrayIcon(State);
        tray.Show();

        if (!State.Settings.FirstRunDone)
        {
            State.Settings.FirstRunDone = true;
            State.Settings.FileSearchFolders = FileIndexer.DefaultFolders();
            State.SaveSettings();
            Autostart.Set(State.Settings.Autostart, AppInfo.ExePath);
            State.OpenSettings("themes");
        }

        app.Run();
        State.Shutdown();
        _mutex.ReleaseMutex();
        return 0;
    }

    private static void RestoreLockScreenFromBackup()
    {
        try
        {
            var snap = SystemThemeApplier.LoadSnapshot();
            if (!string.IsNullOrEmpty(snap.LockScreenBackup) && System.IO.File.Exists(snap.LockScreenBackup))
                LockScreenManager.TrySet(snap.LockScreenBackup);
        }
        catch (Exception ex) { Log.Error("restore-lockscreen", ex); }
    }

    public static void Quit()
    {
        Application.Current.Shutdown();
    }
}
