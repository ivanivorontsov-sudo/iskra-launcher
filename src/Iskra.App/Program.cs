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
                    try { return new SystemThemeApplier().Restore(includeHklm: AppInfo.IsAdmin()); } catch (Exception ex) { Log.Error("restore-silent", ex); return 1; }
                case "--ci-apply-theme" when args.Length >= 2:
                    return CiHarness.ApplyTheme(args[1]);
                case "--ci-repair":
                    return CiHarness.Repair();
                case "--icon-batch" when args.Length >= 2:
                    return ShortcutIconApplier.RunBatch(args[1]);
                case "--repair-desktop":
                    try { return DesktopRepair.RunElevatedPart(); } catch (Exception ex) { Log.Error("repair-desktop", ex); return 1; }
                case "--repair-fonts":
                    try { return FontRepairEntry.Run(); } catch (Exception ex) { Log.Error("repair-fonts", ex); return 1; }
            }
        }

        bool created;
        _mutex = new Mutex(true, "Iskra_SingleInstance_8F2A", out created);
        if (!created)
        {
            try
            {
                using var ev = EventWaitHandle.OpenExisting("Iskra_Show_8F2A");
                ev.Set();
            }
            catch { }
            return 0;
        }
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Iskra_Show_8F2A");

        Paths.Init();
        RunState.Begin();                 // маркер сеанса (для определения аварий)
        SafeMode.Evaluate();              // Shift при старте или падение в прошлый раз

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Необработанное исключение", e.ExceptionObject as Exception);
        app.DispatcherUnhandledException += (_, e) => { Log.Error("Исключение UI", e.Exception); e.Handled = true; };

        // Программная отрисовка, если GPU недоступен (Tier 0) или в безопасном режиме — спасает от «чёрного/белого» окна.
        try
        {
            if (SafeMode.Active || (System.Windows.Media.RenderCapability.Tier >> 16) < 1)
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        }
        catch { }

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
        ExplorerRestarter.ExplorerRestarted += () => tray.Reappear();

        var showPump = new Thread(() =>
        {
            while (showEvent.WaitOne())
            {
                try { app.Dispatcher.BeginInvoke(() => { try { State.ShowLauncher(); } catch (Exception ex) { Log.Error("Показ окна", ex); } }); }
                catch { }
            }
        }) { IsBackground = true, Name = "IskraShow" };
        showPump.Start();

        if (!State.Settings.FirstRunDone)
        {
            State.Settings.FirstRunDone = true;
            State.Settings.FileSearchFolders = FileIndexer.DefaultFolders();
            State.SaveSettings();
            Autostart.Set(State.Settings.Autostart, AppInfo.ExePath);
            State.OpenSettings("themes");
        }
        else if (!args.Contains("--tray"))
        {
            try { State.ShowLauncher(); } catch (Exception ex) { Log.Error("Показ окна", ex); }
        }

        app.Run();
        State.Shutdown();
        RunState.End();
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
