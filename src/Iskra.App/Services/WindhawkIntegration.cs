using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Iskra.Core.Themes;

namespace Iskra.App.Services;

/// <summary>
/// НЕОБЯЗАТЕЛЬНАЯ интеграция с официальным Windhawk (windhawk.net) для глубокой стилизации
/// панели задач, «Пуска», центра уведомлений и контекстного меню — того, что нельзя через реестр.
/// Выключена по умолчанию, ставится ТОЛЬКО по явному согласию пользователя и полностью обратима
/// (можно выключить моды или удалить Windhawk). Используются лишь открытые моды из официального каталога.
/// Ничего не делается автоматически — методы вызываются из экрана настроек после подтверждения.
/// </summary>
internal static class WindhawkIntegration
{
    // Официальный установщик (NSIS). Тихая установка: /S /STANDARD.
    private const string InstallerUrl = "https://github.com/ramensoftware/windhawk/releases/latest/download/windhawk_setup.exe";

    // Только открытые моды из официального каталога.
    public static readonly string[] StylerMods =
    {
        "windows-11-taskbar-styler",
        "windows-11-start-menu-styler",
        "windows-11-notification-center-styler",
        "classic-context-menus-styler",
    };

    public static bool IsInstalled()
    {
        try
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return File.Exists(Path.Combine(pf, "Windhawk", "windhawk.exe")) ||
                   File.Exists(Path.Combine(pf, "Windhawk", "windhawk-cli.exe"));
        }
        catch { return false; }
    }

    public static string? CliPath()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var p = Path.Combine(pf, "Windhawk", "windhawk-cli.exe");
        return File.Exists(p) ? p : null;
    }

    /// <summary>Скачивает и тихо ставит официальный Windhawk (нужны права администратора — будет запрос UAC).</summary>
    public static async Task<bool> InstallAsync()
    {
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "windhawk_setup.exe");
            using (var http = new HttpClient())
            using (var resp = await http.GetAsync(InstallerUrl))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(tmp);
                await resp.Content.CopyToAsync(fs);
            }
            var psi = new ProcessStartInfo { FileName = tmp, Arguments = "/S /STANDARD", UseShellExecute = true, Verb = "runas" };
            var p = Process.Start(psi);
            if (p == null) return false;
            await p.WaitForExitAsync();
            return IsInstalled();
        }
        catch (Exception ex) { Log.Error("Установка Windhawk", ex); return false; }
    }

    /// <summary>Включает официальные стилизующие моды под тему. Возвращает число применённых.</summary>
    public static int ApplyThemeMods(ThemeDefinition theme)
    {
        var cli = CliPath();
        if (cli == null) return 0;
        int ok = 0;
        foreach (var mod in StylerMods)
        {
            if (Run(cli, $"mod install {mod}") || Run(cli, $"mod enable {mod}")) ok++;
        }
        // Пример конфигурации под акцент темы (панель задач).
        var accent = Iskra.Core.Windows.Argb.Parse(theme.Palette.Accent).ToHexRgb();
        Run(cli, $"mod settings set windows-11-taskbar-styler ControlStyles[0].styles[0]=\"Fill:{accent}\"");
        return ok;
    }

    /// <summary>Отключает все включённые Искрой моды (откат без удаления Windhawk).</summary>
    public static void DisableMods()
    {
        var cli = CliPath();
        if (cli == null) return;
        foreach (var mod in StylerMods) Run(cli, $"mod disable {mod}");
    }

    private static bool Run(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(20000);
            return p.ExitCode == 0;
        }
        catch (Exception ex) { Log.Warn($"Windhawk CLI «{args}»: {ex.Message}"); return false; }
    }
}
