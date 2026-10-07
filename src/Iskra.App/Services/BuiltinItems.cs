using System;
using System.Collections.Generic;
using Iskra.Core.Search;
using Iskra.Core.Loc;

namespace Iskra.App.Services;

/// <summary>Страницы параметров (ms-settings:) и системные команды — без зависимости от cmd/regedit.</summary>
internal static class BuiltinItems
{
    public static List<SearchItem> Settings()
    {
        var kindS = L.T("kind.Setting");
        (string id, string ru, string en, string uri, string[] kw)[] defs =
        {
            ("home", "Параметры", "Settings", "ms-settings:", new[] { "settings", "nastrojki" }),
            ("display", "Дисплей", "Display", "ms-settings:display", new[] { "экран", "screen", "яркость", "разрешение", "resolution" }),
            ("sound", "Звук", "Sound", "ms-settings:sound", new[] { "громкость", "volume", "динамики", "микрофон" }),
            ("notifications", "Уведомления", "Notifications", "ms-settings:notifications", new[] { "notifications" }),
            ("powersleep", "Питание и батарея", "Power & battery", "ms-settings:powersleep", new[] { "батарея", "battery", "сон", "энергосбережение" }),
            ("bluetooth", "Bluetooth и устройства", "Bluetooth & devices", "ms-settings:bluetooth", new[] { "блютуз", "устройства", "devices" }),
            ("network", "Сеть и интернет", "Network & internet", "ms-settings:network", new[] { "wifi", "вайфай", "интернет", "сеть" }),
            ("personalization", "Персонализация", "Personalization", "ms-settings:personalization", new[] { "тема", "обои", "theme", "wallpaper" }),
            ("colors", "Цвета", "Colours", "ms-settings:colors", new[] { "акцент", "тёмная тема", "dark", "accent" }),
            ("lockscreen", "Экран блокировки", "Lock screen", "ms-settings:lockscreen", new[] { "lock" }),
            ("themes", "Темы", "Themes", "ms-settings:themes", Array.Empty<string>()),
            ("apps", "Приложения", "Apps", "ms-settings:appsfeatures", new[] { "программы", "удалить программу", "uninstall" }),
            ("defaultapps", "Приложения по умолчанию", "Default apps", "ms-settings:defaultapps", new[] { "браузер по умолчанию" }),
            ("startup", "Автозагрузка", "Startup apps", "ms-settings:startupapps", new[] { "startup", "автозапуск" }),
            ("windowsupdate", "Центр обновления Windows", "Windows Update", "ms-settings:windowsupdate", new[] { "обновления", "update" }),
            ("about", "О системе", "About", "ms-settings:about", new[] { "система", "версия", "характеристики" }),
            ("storage", "Память устройства", "Storage", "ms-settings:storagesense", new[] { "диск", "место", "очистка" }),
            ("privacy", "Конфиденциальность", "Privacy", "ms-settings:privacy", new[] { "камера", "микрофон", "разрешения" }),
            ("accounts", "Учётные записи", "Accounts", "ms-settings:yourinfo", new[] { "аккаунт", "пользователь" }),
            ("datetime", "Дата и время", "Date & time", "ms-settings:dateandtime", new[] { "часы", "часовой пояс", "time" }),
            ("language", "Язык и регион", "Language", "ms-settings:regionlanguage", new[] { "раскладка", "язык", "keyboard" }),
            ("mouse", "Мышь", "Mouse", "ms-settings:mousetouchpad", new[] { "курсор", "указатель", "cursor" }),
            ("taskbar", "Панель задач", "Taskbar", "ms-settings:taskbar", new[] { "taskbar" }),
            ("multitasking", "Многозадачность", "Multitasking", "ms-settings:multitasking", new[] { "окна", "snap" }),
            ("bluetoothdevices", "Принтеры и сканеры", "Printers & scanners", "ms-settings:printers", new[] { "принтер", "printer" }),
            ("recovery", "Восстановление", "Recovery", "ms-settings:recovery", new[] { "сброс", "reset", "точка восстановления" }),
        };
        var list = new List<SearchItem>();
        foreach (var d in defs)
            list.Add(new SearchItem("set:" + d.id, L.English ? d.en : d.ru, ItemKind.Setting, d.uri, kindS,
                new[] { d.en, d.ru }.AsReadOnlySpanConcat(d.kw)) { Glyph = "\uE713" });
        return list;
    }

    public enum Cmd { Lock, Sleep, Hibernate, Restart, Shutdown, SignOut, EmptyBin, OpenBin, RestartExplorer, Screenshot, DarkToggle,
        IskraSettings, IskraRestore, IskraNextTheme, IskraStartScreen }

    public static List<SearchItem> Commands()
    {
        var kindC = L.T("kind.Command");
        (Cmd c, string key, string glyph, bool confirm, string[] kw)[] defs =
        {
            (Cmd.Lock, "cmd.lock", "\uE72E", false, new[] { "lock", "заблокировать" }),
            (Cmd.Sleep, "cmd.sleep", "\uE708", false, new[] { "sleep", "сон" }),
            (Cmd.Hibernate, "cmd.hibernate", "\uE707", false, new[] { "hibernate", "гибернация" }),
            (Cmd.Restart, "cmd.restart", "\uE777", true, new[] { "restart", "reboot", "перезагрузка" }),
            (Cmd.Shutdown, "cmd.shutdown", "\uE7E8", true, new[] { "shutdown", "выключить", "завершение" }),
            (Cmd.SignOut, "cmd.signout", "\uE77B", true, new[] { "logoff", "выход", "sign out" }),
            (Cmd.EmptyBin, "cmd.emptybin", "\uE74D", true, new[] { "recycle", "корзина", "очистить" }),
            (Cmd.OpenBin, "cmd.openbin", "\uE74D", false, new[] { "recycle bin", "корзина" }),
            (Cmd.RestartExplorer, "cmd.restartexplorer", "\uE7B8", false, new[] { "explorer", "проводник", "перезапуск" }),
            (Cmd.Screenshot, "cmd.screenshot", "\uE722", false, new[] { "screenshot", "снимок", "скриншот" }),
            (Cmd.DarkToggle, "cmd.darkmode", "\uE793", false, new[] { "dark", "light", "тёмная", "светлая" }),
            (Cmd.IskraSettings, "cmd.iskra.settings", "\uE713", false, new[] { "iskra", "искра", "настройки", "settings" }),
            (Cmd.IskraRestore, "cmd.iskra.restore", "\uE7A7", true, new[] { "restore", "вернуть", "откат" }),
            (Cmd.IskraNextTheme, "cmd.iskra.nexttheme", "\uE790", false, new[] { "theme", "тема", "сменить" }),
            (Cmd.IskraStartScreen, "cmd.iskra.startscreen", "\uE71D", false, new[] { "start screen", "стартовый экран" }),
        };
        var list = new List<SearchItem>();
        foreach (var d in defs)
            list.Add(new SearchItem("do:" + d.c, L.T(d.key), ItemKind.Command, d.c.ToString(), kindC, d.kw)
            { Glyph = d.glyph, NeedsConfirmation = d.confirm });
        return list;
    }
}

internal static class SpanExt
{
    public static IEnumerable<string> AsReadOnlySpanConcat(this string[] a, string[] b)
    { foreach (var x in a) yield return x; foreach (var y in b) yield return y; }
}
