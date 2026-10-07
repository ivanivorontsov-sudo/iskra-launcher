using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Iskra.Core.Search;

namespace Iskra.App.Services;

/// <summary>Запуск элементов поиска: приложения, файлы, параметры, веб и системные команды.</summary>
internal static class Launcher
{
    public static void Launch(SearchItem item, bool asAdmin = false, bool openLocation = false)
    {
        try
        {
            if (openLocation && item.FilePath != null) { RevealInExplorer(item.FilePath); return; }
            switch (item.Kind)
            {
                case ItemKind.App:
                case ItemKind.StoreApp:
                case ItemKind.File:
                case ItemKind.Folder:
                    Start(item.Target, asAdmin);
                    break;
                case ItemKind.Setting:
                case ItemKind.Web:
                    Start(item.Target, false);
                    break;
                case ItemKind.Calculator:
                    try { Clipboard.SetText(item.Target); } catch { }
                    break;
                case ItemKind.Command:
                    SystemCommands.Run(Enum.Parse<BuiltinItems.Cmd>(item.Target));
                    break;
                case ItemKind.Theme:
                    break;
            }
        }
        catch (Exception ex) { Log.Error("Запуск " + item, ex); }
    }

    public static void Start(string target, bool asAdmin)
    {
        var psi = new ProcessStartInfo { FileName = target, UseShellExecute = true };
        if (asAdmin) psi.Verb = "runas";
        Process.Start(psi);
    }

    public static void RevealInExplorer(string path)
    {
        if (File.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        else if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}
