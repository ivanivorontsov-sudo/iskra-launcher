using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>
/// Запись разрешённых HKLM-значений темы одним повышенным процессом (--hklm-writes файл.json).
/// Исходные значения снимает непривилегированный процесс ДО записи; здесь — только запись, каждая через RegistryGuard.
/// </summary>
internal static class HklmWriter
{
    private sealed record Item(string Key, string Name, RegKind Kind, string? Value, string Category);

    public static bool RunElevated(List<RegWrite> writes)
    {
        try
        {
            var file = Path.Combine(Paths.Root, "hklm-writes.json");
            var items = writes.ConvertAll(w => new Item(w.Key, w.Name, w.Kind, w.Value?.ToString(), w.Category));
            File.WriteAllText(file, JsonSerializer.Serialize(items));
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = AppInfo.ExePath, Arguments = $"--hklm-writes \"{file}\"",
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (p == null || !p.WaitForExit(60000)) return false;
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { Log.Warn("HKLM: UAC отклонён"); return false; }
        catch (Exception ex) { Log.Error("HKLM writes", ex); return false; }
    }

    public static int Run(string file)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<Item>>(File.ReadAllText(file)) ?? new();
            var applier = new RegistryApplier(new SystemSnapshot()); // снимок здесь не ведём — он уже снят
            foreach (var i in items)
            {
                // Строковые значения + удаление; только то, что разрешит охранник (Shell Icons\29, шрифты…).
                if (i.Kind is not (RegKind.String or RegKind.DeleteValue)) continue;
                applier.ApplyOne(new RegWrite(RegHive.LocalMachine, i.Key, i.Name, i.Kind, i.Value, i.Category));
            }
            try { File.Delete(file); } catch { }
            return 0;
        }
        catch (Exception ex) { Log.Error("HklmWriter.Run", ex); return 1; }
    }
}
