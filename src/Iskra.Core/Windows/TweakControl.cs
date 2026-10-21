namespace Iskra.Core.Windows;

/// <summary>
/// Управление отдельными настройками Windows: что выключено (и должно быть откачено), какие значения
/// заблокированы групповой политикой / MDM (Windows 11 Enterprise), и проверка, что запись реально применилась.
/// Чистые функции — проверяются тестами на любой ОС.
/// </summary>
public static class TweakControl
{
    /// <summary>Категория снимка → включена ли она в настройках.</summary>
    public static IReadOnlyDictionary<string, bool> CategoryStates(SystemApplyOptions o) => new Dictionary<string, bool>
    {
        ["colors"] = o.Colors,
        ["classiccolors"] = o.Colors && o.ClassicColors,
        ["cursors"] = o.Cursors,
        ["sounds"] = o.Sounds,
        ["icons"] = o.IconSpacing,
        ["contextmenu"] = o.ContextMenu,
        ["explorer"] = o.Explorer,
        ["taskbar"] = o.Taskbar,
        ["metrics"] = o.WindowMetrics,
        ["shellicons"] = o.ShellIcons,
        ["arrow"] = o.HideShortcutArrows,
    };

    /// <summary>Категории, которые сейчас выключены — их прошлые изменения надо вернуть «как было».</summary>
    public static List<string> DisabledCategories(SystemApplyOptions o)
        => CategoryStates(o).Where(kv => !kv.Value).Select(kv => kv.Key).ToList();

    /// <summary>Относится ли запись снимка к категории (с учётом старых снимков, где классические цвета и стрелка лежали в общих категориях).</summary>
    public static bool BelongsTo(RegOriginal r, string category)
    {
        bool classic = string.Equals(RegistryGuard.Normalize(r.Key), SystemPlan.ColorsKey, StringComparison.OrdinalIgnoreCase);
        bool arrow = r.Hive == RegHive.LocalMachine && string.Equals(RegistryGuard.Normalize(r.Key), SystemPlan.ShellIconsKeyLm, StringComparison.OrdinalIgnoreCase) && r.Name == "29";
        return category switch
        {
            "classiccolors" => classic,
            "colors" => r.Category == "colors" && !classic,
            "arrow" => arrow || r.Category == "arrow",
            "shellicons" => r.Category == "shellicons" && !arrow,
            _ => r.Category == category,
        };
    }

    /// <summary>Вынимает из снимка все записи категорий (снимок больше их не помнит — они будут откатаны).</summary>
    public static List<RegOriginal> Take(SystemSnapshot s, IEnumerable<string> categories)
    {
        var cats = categories.ToList();
        var taken = s.Registry.Where(r => cats.Any(c => BelongsTo(r, c))).ToList();
        foreach (var r in taken) s.Registry.Remove(r);
        return taken;
    }

    /// <summary>Исходное значение → запись, возвращающая его (для повышенного процесса HKLM).</summary>
    public static RegWrite ToRestoreWrite(RegOriginal r)
    {
        if (!r.ValueExisted) return new RegWrite(r.Hive, r.Key, r.Name, RegKind.DeleteValue, null, r.Category);
        object? v = r.Kind switch
        {
            RegKind.DWord => (object)(int)(r.NumberValue ?? 0),
            RegKind.Binary => Convert.FromBase64String(r.BinaryBase64 ?? ""),
            RegKind.MultiString => r.MultiValue ?? Array.Empty<string>(),
            _ => r.StringValue ?? "",
        };
        return new RegWrite(r.Hive, r.Key, r.Name, r.Kind, v, r.Category);
    }

    // ---------------- Групповые политики / MDM ----------------

    public sealed record PolicyRule(string Category, RegHive Hive, string Key, string Name, string WhatRu, string WhatEn);
    public sealed record PolicyLock(string Category, string Where, string WhatRu, string WhatEn);

    private const string ExplorerPol = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    private const string SystemPol = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string ActiveDesktopPol = @"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop";
    private const string PersonalizationPol = @"Software\Policies\Microsoft\Windows\Personalization";
    private const string ExplorerPol2 = @"Software\Policies\Microsoft\Windows\Explorer";

    /// <summary>Известные политики, которые запрещают или перекрывают изменения «Искры». Значение ≠ 0 / непустое = заблокировано.</summary>
    public static readonly PolicyRule[] Rules = BuildRules();

    private static PolicyRule[] BuildRules()
    {
        var list = new List<PolicyRule>();
        void Both(string cat, string key, string name, string ru, string en)
        {
            list.Add(new(cat, RegHive.CurrentUser, key, name, ru, en));
            list.Add(new(cat, RegHive.LocalMachine, key, name, ru, en));
        }
        Both("colors", SystemPol, "NoDispAppearancePage", "запрещено менять цвета и оформление", "changing colours/appearance is blocked");
        Both("colors", PersonalizationPol, "NoChangingColor", "запрещено менять цвета", "changing colours is blocked");
        Both("colors", ExplorerPol, "NoThemesTab", "запрещено менять темы", "changing themes is blocked");
        Both("classiccolors", SystemPol, "NoDispAppearancePage", "запрещено менять цвета и оформление", "changing colours/appearance is blocked");
        Both("wallpaper", ActiveDesktopPol, "NoChangingWallPaper", "запрещено менять обои", "changing wallpaper is blocked");
        Both("wallpaper", SystemPol, "Wallpaper", "обои заданы политикой", "wallpaper is set by policy");
        Both("wallpaper", SystemPol, "NoDispBackgroundPage", "запрещено менять фон", "changing background is blocked");
        Both("lockscreen", PersonalizationPol, "NoChangingLockScreen", "запрещено менять экран блокировки", "changing lock screen is blocked");
        Both("lockscreen", PersonalizationPol, "LockScreenImage", "экран блокировки задан политикой", "lock screen image is set by policy");
        Both("cursors", ExplorerPol, "NoChangingMousePointers", "запрещено менять указатели мыши", "changing mouse pointers is blocked");
        Both("sounds", ExplorerPol, "NoChangingSoundScheme", "запрещено менять звуковую схему", "changing sound scheme is blocked");
        Both("contextmenu", ExplorerPol2, "DisableContextMenusInStart", "контекстные меню ограничены политикой", "context menus restricted by policy");
        Both("taskbar", ExplorerPol2, "LockTaskbar", "панель задач заблокирована политикой", "taskbar locked by policy");
        Both("taskbar", ExplorerPol, "TaskbarLockAll", "панель задач заблокирована политикой", "taskbar locked by policy");
        Both("taskbar", ExplorerPol2, "TaskbarAlignment", "выравнивание панели задач задано политикой", "taskbar alignment set by policy");
        Both("shellicons", ExplorerPol, "NoChangingDesktopIcons", "запрещено менять значки рабочего стола", "changing desktop icons is blocked");
        Both("arrow", ExplorerPol, "NoChangingDesktopIcons", "запрещено менять значки рабочего стола", "changing desktop icons is blocked");
        return list.ToArray();
    }

    /// <summary>reader(hive, key, name) → значение или null. Возвращает найденные блокировки (по одной на категорию+правило).</summary>
    public static List<PolicyLock> DetectLocks(Func<RegHive, string, string, object?> reader)
    {
        var locks = new List<PolicyLock>();
        foreach (var r in Rules)
        {
            object? v;
            try { v = reader(r.Hive, r.Key, r.Name); } catch { continue; }
            if (!IsSet(v)) continue;
            var where = (r.Hive == RegHive.LocalMachine ? "HKLM\\" : "HKCU\\") + r.Key + " [" + r.Name + "]";
            if (!locks.Any(l => l.Category == r.Category && l.WhatRu == r.WhatRu)) locks.Add(new(r.Category, where, r.WhatRu, r.WhatEn));
        }
        return locks;
    }

    private static bool IsSet(object? v) => v switch
    {
        null => false,
        int i => i != 0,
        long l => l != 0,
        string s => s.Length > 0 && s != "0",
        byte[] b => b.Length > 0,
        _ => true,
    };

    /// <summary>
    /// После записи: какие значения не «прилипли» (политика/MDM вернула своё, нет прав, AppLocker/Defender помешал).
    /// Возвращает категории с расхождением.
    /// </summary>
    public static List<string> VerifyWrites(IEnumerable<RegWrite> writes, Func<RegHive, string, string, object?> reader)
    {
        var bad = new List<string>();
        foreach (var w in writes)
        {
            if (w.Kind == RegKind.DeleteKey) continue;
            object? cur;
            try { cur = reader(w.Hive, w.Key, w.Name); } catch { cur = null; }
            bool ok = w.Kind switch
            {
                RegKind.DeleteValue => cur == null,
                RegKind.Binary => cur is byte[] b && w.Value is byte[] e && b.SequenceEqual(e),
                RegKind.MultiString => cur is string[] a && w.Value is string[] e2 && a.SequenceEqual(e2),
                RegKind.DWord => cur != null && Convert.ToInt64(cur) == Convert.ToInt64(w.Value),
                _ => cur != null && string.Equals(cur.ToString(), w.Value?.ToString(), StringComparison.OrdinalIgnoreCase),
            };
            if (!ok && !bad.Contains(w.Category)) bad.Add(w.Category);
        }
        return bad;
    }
}
