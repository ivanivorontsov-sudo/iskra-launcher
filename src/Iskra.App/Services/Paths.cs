using System;
using System.IO;

namespace Iskra.App.Services;

/// <summary>Все данные — в %LOCALAPPDATA%\Iskra (установка и работа без прав администратора).</summary>
internal static class Paths
{
    public static string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Iskra");
    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Usage => Path.Combine(Root, "usage.json");
    public static string Snapshot => Path.Combine(Root, "system-snapshot.json");
    public static string RestoreScript => Path.Combine(Root, "Вернуть-как-было.ps1");
    public static string Cache => Ensure(Path.Combine(Root, "cache"));
    public static string Generated => Ensure(Path.Combine(Root, "generated"));
    public static string Cursors => Ensure(Path.Combine(Generated, "cursors"));
    public static string Sounds => Ensure(Path.Combine(Generated, "sounds"));
    public static string Wallpapers => Ensure(Path.Combine(Generated, "wallpapers"));
    public static string FontsForSystem => Ensure(Path.Combine(Root, "fonts"));
    public static string LockScreenBackup => Path.Combine(Root, "lockscreen-backup");
    public static string Log => Path.Combine(Root, "iskra.log");

    public static string Ensure(string dir) { Directory.CreateDirectory(dir); return dir; }

    public static void Init() => Directory.CreateDirectory(Root);
}
