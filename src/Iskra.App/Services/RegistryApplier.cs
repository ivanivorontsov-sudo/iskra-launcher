using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>
/// Пишет значения в реестр через .NET Registry API (НЕ reg.exe/regedit, которые блокирует «Стоп-экран»).
/// Перед каждой первой записью значения фиксирует «как было» в снимок. Всё проходит через <see cref="RegistryGuard"/>.
/// </summary>
internal sealed class RegistryApplier
{
    private readonly SystemSnapshot _snapshot;
    public RegistryApplier(SystemSnapshot snapshot) => _snapshot = snapshot;

    private static RegistryKey Root(RegHive h) => h == RegHive.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;

    public void Apply(IEnumerable<RegWrite> writes)
    {
        foreach (var w in writes) ApplyOne(w);
    }

    /// <summary>Снять исходное значение в снимок, НИЧЕГО не записывая (для живого применения через SPI).</summary>
    public void CaptureValue(RegHive hive, string key, string name, string category)
        => CaptureOriginal(new RegWrite(hive, key, name, RegKind.Binary, null, category));

    public void ApplyOne(RegWrite w)
    {
        if (!RegistryGuard.IsAllowed(w))
        {
            Log.Warn($"Запрет охранника реестра: {w}");
            return;
        }
        CaptureOriginal(w);
        try
        {
            if (w.Kind == RegKind.DeleteKey) { Root(w.Hive).DeleteSubKeyTree(w.Key, throwOnMissingSubKey: false); return; }
            using var key = Root(w.Hive).CreateSubKey(w.Key, writable: true)!;
            if (w.Kind == RegKind.DeleteValue) { key.DeleteValue(w.Name, throwOnMissingValue: false); return; }
            key.SetValue(w.Name, Coerce(w), ToKind(w.Kind));
        }
        catch (Exception ex) { Log.Error($"Запись реестра {w}", ex); }
    }

    private static object Coerce(RegWrite w) => w.Kind switch
    {
        RegKind.DWord => Convert.ToInt32(w.Value),
        RegKind.Binary => (byte[])w.Value!,
        RegKind.MultiString => (string[])w.Value!,
        _ => w.Value?.ToString() ?? string.Empty,
    };

    private static RegistryValueKind ToKind(RegKind k) => k switch
    {
        RegKind.DWord => RegistryValueKind.DWord,
        RegKind.ExpandString => RegistryValueKind.ExpandString,
        RegKind.Binary => RegistryValueKind.Binary,
        RegKind.MultiString => RegistryValueKind.MultiString,
        _ => RegistryValueKind.String,
    };

    /// <summary>Сохраняем исходное значение один раз. Для удаляемого ключа — всё поддерево значений.</summary>
    private void CaptureOriginal(RegWrite w)
    {
        if (_snapshot.Contains(w.Hive, w.Key, w.Name)) return;
        var orig = new RegOriginal { Hive = w.Hive, Key = w.Key, Name = w.Name, Category = w.Category };
        if (w.Hive == RegHive.LocalMachine) _snapshot.HklmTouched = true;

        var createdRoot = FindCreatedRoot(w.Hive, w.Key);
        orig.KeyTreeExisted = createdRoot == null;
        orig.CreatedKeyRoot = createdRoot;

        if (w.Kind == RegKind.DeleteKey)
        {
            orig.Subtree = SnapshotSubtree(w.Hive, w.Key);
            _snapshot.Registry.Add(orig);
            return;
        }

        using var key = Root(w.Hive).OpenSubKey(w.Key);
        if (key == null) { orig.ValueExisted = false; _snapshot.Registry.Add(orig); return; }
        var value = key.GetValue(w.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value == null) { orig.ValueExisted = false; _snapshot.Registry.Add(orig); return; }
        orig.ValueExisted = true;
        orig.Kind = FromKind(key.GetValueKind(w.Name));
        switch (orig.Kind)
        {
            case RegKind.DWord: orig.NumberValue = Convert.ToInt64(value); break;
            case RegKind.Binary: orig.BinaryBase64 = Convert.ToBase64String((byte[])value); break;
            case RegKind.MultiString: orig.MultiValue = (string[])value; break;
            default: orig.StringValue = value.ToString(); break;
        }
        _snapshot.Registry.Add(orig);
    }

    /// <summary>Снимок удаляемого ключа ЦЕЛИКОМ: значения и вложенные ключи (раньше вложенные терялись —
    /// например InprocServer32 классического меню, и откат его не возвращал).</summary>
    private List<RegOriginal> SnapshotSubtree(RegHive hive, string key, int depth = 0)
    {
        var list = new List<RegOriginal>();
        using var k = Root(hive).OpenSubKey(key);
        if (k == null || depth > 8) return list;
        foreach (var name in k.GetValueNames())
        {
            var val = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (val == null) continue;
            var o = new RegOriginal { Hive = hive, Key = key, Name = name, ValueExisted = true, Kind = FromKind(k.GetValueKind(name)) };
            switch (o.Kind)
            {
                case RegKind.DWord: o.NumberValue = Convert.ToInt64(val); break;
                case RegKind.Binary: o.BinaryBase64 = Convert.ToBase64String((byte[])val); break;
                case RegKind.MultiString: o.MultiValue = (string[])val; break;
                default: o.StringValue = val.ToString(); break;
            }
            list.Add(o);
        }
        foreach (var sub in k.GetSubKeyNames())
        {
            var child = key + "\\" + sub;
            list.Add(new RegOriginal { Hive = hive, Key = child, Name = "", KeyTreeExisted = true, Subtree = SnapshotSubtree(hive, child, depth + 1) });
        }
        return list;
    }

    /// <summary>Чтение значения для проверки записи и поиска политик.</summary>
    public static object? Read(RegHive hive, string key, string name)
    {
        using var k = Root(hive).OpenSubKey(key);
        return k?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
    }

    /// <summary>Верхний из отсутствующих ключей в цепочке (его целиком удалим при откате).</summary>
    private static string? FindCreatedRoot(RegHive hive, string key)
    {
        var parts = key.Split('\\');
        string path = "";
        for (int i = 0; i < parts.Length; i++)
        {
            path = i == 0 ? parts[0] : path + "\\" + parts[i];
            using var k = Root(hive).OpenSubKey(path);
            if (k == null) return path;
        }
        return null;
    }

    private static RegKind FromKind(RegistryValueKind k) => k switch
    {
        RegistryValueKind.DWord => RegKind.DWord,
        RegistryValueKind.ExpandString => RegKind.ExpandString,
        RegistryValueKind.Binary => RegKind.Binary,
        RegistryValueKind.MultiString => RegKind.MultiString,
        _ => RegKind.String,
    };

    /// <summary>Откат снимка прямо из приложения (быстрый путь, без скрипта). Возвращает число ошибок.</summary>
    public static int Restore(SystemSnapshot s, bool includeHklm) => Restore(s.Registry, includeHklm);

    public static int Restore(IEnumerable<RegOriginal> originals, bool includeHklm)
    {
        int errors = 0;
        var all = originals.ToList();
        // Значения сначала, удаление созданных ключей — в конце (от длинных к коротким).
        foreach (var r in all.Where(r => includeHklm || r.Hive == RegHive.CurrentUser))
        {
            try { RestoreOne(r); } catch (Exception ex) { errors++; Log.Error($"Откат {r.Hive}\\{r.Key} [{r.Name}]", ex); }
        }
        foreach (var r in all.Where(r => (includeHklm || r.Hive == RegHive.CurrentUser) && !r.KeyTreeExisted && r.CreatedKeyRoot != null)
                                    .OrderByDescending(r => r.CreatedKeyRoot!.Length))
        {
            try { Root(r.Hive).DeleteSubKeyTree(r.CreatedKeyRoot!, false); } catch (Exception ex) { errors++; Log.Error($"Удаление ключа {r.CreatedKeyRoot}", ex); }
        }
        return errors;
    }

    private static void RestoreOne(RegOriginal r)
    {
        if (r.Subtree != null)
        {
            if (r.KeyTreeExisted) Root(r.Hive).CreateSubKey(r.Key, true)?.Dispose(); // пустой ключ тоже важен (классическое меню)
            foreach (var sub in r.Subtree) RestoreOne(sub);
            return;
        }
        if (!r.KeyTreeExisted) return; // ключ удалим отдельным проходом
        if (!r.ValueExisted)
        {
            using var k = Root(r.Hive).OpenSubKey(r.Key, true);
            k?.DeleteValue(r.Name, false);
            return;
        }
        using var key = Root(r.Hive).CreateSubKey(r.Key, true)!;
        object val = r.Kind switch
        {
            RegKind.DWord => (int)(r.NumberValue ?? 0),
            RegKind.Binary => Convert.FromBase64String(r.BinaryBase64 ?? ""),
            RegKind.MultiString => r.MultiValue ?? Array.Empty<string>(),
            _ => r.StringValue ?? "",
        };
        key.SetValue(r.Name, val, ToKind(r.Kind));
    }
}
