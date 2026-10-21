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
                    try { return new SystemThemeApplier().RestoreAll().Errors; } /* при удалении: HKCU сразу, HKLM — через UAC */ catch (Exception ex) { Log.Error("restore-silent", ex); return 1; }
                case "--ci-apply-theme" when args.Length >= 2:
                    return CiHarness.ApplyTheme(args[1]);
                case "--ci-repair":
                    return CiHarness.Repair();
                case "--hklm-writes" when args.Length >= 2:
                    return HklmWriter.Run(args[1]);
                case "--icon-batch" when args.Length >= 2:
                    return ShortcutIconApplier.RunBatch(args[1]);
                case "--repair-desktop":
                    try { return DesktopRepair.RunElevatedPart(); } catch (Exception ex) { Log.Error("repair-desktop", ex); return 1; }
                case "--repair-fonts":
                    try { return FontRepairEntry.Run(); } catch (Exception ex) { Log.Error("repair-fonts", ex); return 1; }
            }
        }

        // Сначала создаём событие, затем захватываем mutex: второй запуск не попадёт
        // в окно гонки, когда mutex уже существует, а событие показа ещё не создано.
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Iskra_Show_8F2A");
        using var instanceMutex = new Mutex(false, "Iskra_SingleInstance_8F2A");
        bool ownsMutex;
        try { ownsMutex = instanceMutex.WaitOne(0); }
        catch (AbandonedMutexException) { ownsMutex = true; } // прошлый процесс завершился аварийно

        if (!ownsMutex)
        {
            try { showEvent.Set(); } catch (Exception ex) { Log.Warn("Сигнал уже запущенному экземпляру: " + ex.Message); }
            return 0;
        }

        Paths.Init();
        RunState.Begin();                 // маркер сеанса (для определения аварий)
        try
        {
            bool crashed = RunState.CrashedLastTime;
            SafeMode.Evaluate();          // Shift при старте или падение в прошлый раз
            if (crashed) CrashRestore.Run();

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
            return 0;
        }
        finally
        {
            try { if (State is not null) State.Shutdown(); } catch (Exception ex) { Log.Error("Завершение состояния", ex); }
            RunState.End();
            try { instanceMutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
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

/// <summary>Прошлый сеанс упал — возвращаем Windows как было (только HKCU и файлы, без UAC), если пользователь не выключил это.</summary>
internal static class CrashRestore
{
    public static void Run()
    {
        try
        {
            var settings = Iskra.Core.Settings.AppSettings.LoadOrHeal(Paths.Settings, out _);
            if (settings != null && !settings.RestoreWindowsAfterCrash) return;
            var sys = new SystemThemeApplier();
            if (!sys.HasSnapshot) return;
            int e = sys.Restore(includeHklm: AppInfo.IsAdmin(), clearOnSuccess: true);
            Log.Warn($"Аварийное завершение прошлого сеанса: изменения Windows возвращены (ошибок: {e}).");
        }
        catch (Exception ex) { Log.Error("Откат после сбоя", ex); }
    }
}
