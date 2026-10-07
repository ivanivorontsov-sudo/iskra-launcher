using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>
/// ЭКСПЕРИМЕНТАЛЬНО: меняет системный шрифт Windows через FontSubstitutes (HKLM) — нужны права администратора.
/// Создаёт точку восстановления, пишет через .NET Registry API (без reg.exe) из повышенного процесса Искры,
/// и ставит «страховку»: если после входа пользователь не подтвердит, что текст в порядке, через 5 минут всё откатится.
/// </summary>
internal sealed class SystemFontApplier
{
    private readonly SystemThemeApplier _system;
    public SystemFontApplier(SystemThemeApplier system) => _system = system;

    /// <summary>Запускает повышенный процесс, который применит шрифт. Возвращает false, если пользователь отказал в UAC.</summary>
    public bool ApplyElevated(string familyName)
    {
        var file = FontInstaller.ExtractForSystem(familyName);
        if (file == null) return false;
        var fontFileName = InstallFontForAllUsers(file);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = AppInfo.ExePath,
                Arguments = $"--apply-system-font \"{familyName}\" \"{fontFileName}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var p = Process.Start(psi);
            p?.WaitForExit(60000);
            return p?.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; } // пользователь отменил UAC
        catch (Exception ex) { Log.Error("Повышение для шрифта", ex); return false; }
    }

    public bool RevertElevated()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = AppInfo.ExePath,
                Arguments = "--revert-system-font",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var p = Process.Start(psi);
            p?.WaitForExit(60000);
            return p?.ExitCode == 0;
        }
        catch (Exception ex) { Log.Error("Откат шрифта", ex); return false; }
    }

    /// <summary>Копирует .ttf в общий Fonts и возвращает имя файла для реестра. Требует прав администратора на запись в Windows\Fonts.</summary>
    private static string InstallFontForAllUsers(string ttfPath) => Path.GetFileName(ttfPath);

    // ---- Вызывается из повышенного процесса ----

    public static int RunElevatedApply(string familyName, string fontFileName)
    {
        try
        {
            RestorePoint.Create("Искра: системный шрифт");
            // Копируем шрифт в C:\Windows\Fonts (есть права администратора).
            var fontsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
            var local = Path.Combine(Paths.FontsForSystem, fontFileName);
            var dst = Path.Combine(fontsDir, fontFileName);
            if (File.Exists(local) && !File.Exists(dst)) File.Copy(local, dst, true);

            var snapshot = SystemThemeApplier.LoadSnapshot();
            var applier = new RegistryApplier(snapshot);
            applier.Apply(SystemPlan.BuildSystemFont(familyName, fontFileName));
            snapshot.HklmTouched = true;
            Iskra.Core.Settings.AppSettings.SaveAtomic(Paths.Snapshot, snapshot.ToJson());
            File.WriteAllText(Paths.RestoreScript, RestoreScriptGenerator.Generate(snapshot, AppInfo.ExePath), new System.Text.UTF8Encoding(true));
            ThemeBroadcast.Notify();
            return 0;
        }
        catch (Exception ex) { Log.Error("RunElevatedApply", ex); return 1; }
    }

    public static int RunElevatedRevert()
    {
        try
        {
            var snapshot = SystemThemeApplier.LoadSnapshot();
            RegistryApplier.Restore(snapshot, includeHklm: true);
            // Удаляем из снимка записи шрифта, чтобы не откатывать их повторно.
            snapshot.Registry.RemoveAll(r => r.Category == "font" || r.Key.Contains("Fonts", StringComparison.OrdinalIgnoreCase));
            Iskra.Core.Settings.AppSettings.SaveAtomic(Paths.Snapshot, snapshot.ToJson());
            ThemeBroadcast.Notify();
            return 0;
        }
        catch (Exception ex) { Log.Error("RunElevatedRevert", ex); return 1; }
    }
}

/// <summary>Точка восстановления системы через WMI (SystemRestore.CreateRestorePoint). Требует прав администратора.</summary>
internal static class RestorePoint
{
    public static void Create(string description)
    {
        try
        {
            using var mc = new System.Management.ManagementClass(@"\\localhost\root\default", "SystemRestore", null);
            var inParams = mc.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description;
            inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
            inParams["EventType"] = 100; // BEGIN_SYSTEM_CHANGE
            mc.InvokeMethod("CreateRestorePoint", inParams, null);
        }
        catch (Exception ex) { Log.Warn("Точка восстановления не создана: " + ex.Message); }
    }
}
