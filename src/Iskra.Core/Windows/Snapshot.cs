using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Iskra.Core.Windows;

/// <summary>Исходное значение реестра до первого изменения «Искрой».</summary>
public sealed class RegOriginal
{
    public RegHive Hive { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public bool ValueExisted { get; set; }
    /// <summary>Самый верхний ключ, которого не было и который создадим мы (удалить при откате).</summary>
    public string? CreatedKeyRoot { get; set; }
    public bool KeyTreeExisted { get; set; } = true;
    public RegKind Kind { get; set; }
    public string? StringValue { get; set; }
    public long? NumberValue { get; set; }
    public string? BinaryBase64 { get; set; }
    public string[]? MultiValue { get; set; }
    /// <summary>Для DeleteKey: сохранённое поддерево (значения), чтобы восстановить.</summary>
    public List<RegOriginal>? Subtree { get; set; }

    [JsonIgnore] public string Id => $"{Hive}|{Key.ToLowerInvariant()}|{Name.ToLowerInvariant()}";
}

public sealed class WallpaperOriginal
{
    public List<string> MonitorIds { get; set; } = new();
    public List<string> Paths { get; set; } = new();
    public int Position { get; set; } = 4; // DWPOS_FILL
    public uint BackgroundColor { get; set; }
    public bool SlideshowActive { get; set; }
    public List<string> SlideshowItems { get; set; } = new();
    public uint SlideshowTickMs { get; set; }
    public bool SlideshowShuffle { get; set; }
}

/// <summary>Резервная копия исходного значка ярлыка (.lnk), чтобы вернуть его при откате.</summary>
public sealed class ShortcutIconBackup
{
    public string LnkPath { get; set; } = "";
    /// <summary>Исходный IconLocation в формате "путь,индекс" ("" — не задан, брался значок цели).</summary>
    public string OriginalIconLocation { get; set; } = "";
}

/// <summary>Резервная копия значка .url или папки (desktop.ini) на рабочем столе.</summary>
public sealed class FileIconBackup
{
    public string Path { get; set; } = "";
    /// <summary>"url" или "folder".</summary>
    public string Kind { get; set; } = "";
    /// <summary>Было ли (для папки — desktop.ini, для .url — строки IconFile/IconIndex).</summary>
    public bool Existed { get; set; }
    /// <summary>Исходное содержимое desktop.ini / значения IconFile,IconIndex ("файл|индекс").</summary>
    public string? Original { get; set; }
    /// <summary>Исходные атрибуты папки (для desktop.ini нужен ReadOnly/System).</summary>
    public int Attributes { get; set; }
}

/// <summary>Снимок «как было» до первого применения темы к Windows. Каждое значение записывается один раз.</summary>
public sealed class SystemSnapshot
{
    public int Version { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string MachineName { get; set; } = Environment.MachineName;
    public string UserName { get; set; } = Environment.UserName;
    public List<RegOriginal> Registry { get; set; } = new();
    public WallpaperOriginal? Wallpaper { get; set; }
    /// <summary>Копия исходной картинки экрана блокировки.</summary>
    public string? LockScreenBackup { get; set; }
    public bool LockScreenTouched { get; set; }
    /// <summary>Исходное состояние «автоматически скрывать панель задач» (если мы его меняли).</summary>
    public bool? TaskbarAutoHide { get; set; }
    public bool CursorsTouched { get; set; }
    public bool IconSpacingTouched { get; set; }
    public bool ContextMenuTouched { get; set; }
    public bool HklmTouched { get; set; }
    public bool ShellIconsTouched { get; set; }
    public List<ShortcutIconBackup> Shortcuts { get; set; } = new();
    public List<FileIconBackup> FileIcons { get; set; } = new();
    public string? RestorePointResult { get; set; }

    public bool Contains(RegHive hive, string key, string name)
    {
        var id = $"{hive}|{key.ToLowerInvariant()}|{name.ToLowerInvariant()}";
        return Registry.Any(r => r.Id == id);
    }

    public bool IsEmpty => Registry.Count == 0 && Wallpaper == null && !LockScreenTouched && TaskbarAutoHide == null && Shortcuts.Count == 0 && FileIcons.Count == 0;

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static SystemSnapshot? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<SystemSnapshot>(json, Json); } catch (JsonException) { return null; }
    }
}

/// <summary>
/// Генерирует самостоятельный PowerShell-скрипт «Вернуть как было». Работает без «Искры»,
/// использует только .NET Registry API (не reg.exe/regedit, которые блокирует «Стоп-экран»).
/// </summary>
public static class RestoreScriptGenerator
{
    public static string Generate(SystemSnapshot s, string? appExePath = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Искра — вернуть Windows как было");
        sb.AppendLine($"# Снимок создан: {s.CreatedUtc:yyyy-MM-dd HH:mm} UTC, компьютер {s.MachineName}, пользователь {s.UserName}");
        sb.AppendLine("# Запуск: правый клик → «Выполнить с помощью PowerShell»  или  powershell -ExecutionPolicy Bypass -File <этот файл>");
        sb.AppendLine("# Скрипт использует только .NET Registry API — внешние редакторы реестра не нужны.");
        sb.AppendLine("param([switch]$HklmOnly)");
        sb.AppendLine("$ErrorActionPreference = 'Continue'");
        sb.AppendLine("[Console]::OutputEncoding = [Text.Encoding]::UTF8");
        sb.AppendLine("$K = [Microsoft.Win32.RegistryValueKind]");
        sb.AppendLine("function RootOf($h) { if ($h -eq 'HKLM') { [Microsoft.Win32.Registry]::LocalMachine } else { [Microsoft.Win32.Registry]::CurrentUser } }");
        sb.AppendLine("function SetV($h, $key, $name, $value, $kind) { try { $k = (RootOf $h).CreateSubKey($key); $k.SetValue($name, $value, $kind); $k.Close() } catch { Write-Warning \"$h\\${key} [${name}]: $_\" } }");
        sb.AppendLine("function DelV($h, $key, $name) { try { $k = (RootOf $h).OpenSubKey($key, $true); if ($k) { $k.DeleteValue($name, $false); $k.Close() } } catch { Write-Warning \"$h\\${key} [${name}]: $_\" } }");
        sb.AppendLine("function DelK($h, $key) { try { (RootOf $h).DeleteSubKeyTree($key, $false) } catch { Write-Warning \"$h\\${key}: $_\" } }");
        sb.AppendLine();

        var hkcu = s.Registry.Where(r => r.Hive == RegHive.CurrentUser).ToList();
        var hklm = s.Registry.Where(r => r.Hive == RegHive.LocalMachine).ToList();

        if (hklm.Count > 0)
        {
            sb.AppendLine("# --- Системная часть (HKLM): нужны права администратора ---");
            sb.AppendLine("$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)");
            sb.AppendLine("if ($isAdmin) {");
            foreach (var r in hklm) EmitRestore(sb, r, "  ");
            // Починка семейства Segoe UI и удаление подстановки — работает даже если в снимке чего-то не хватает.
            sb.AppendLine("  # Возврат штатной регистрации Segoe UI и удаление подстановки базового шрифта");
            foreach (var (name, file) in FontPlan.SegoeDefaults)
                sb.AppendLine($"  SetV 'HKLM' {Ps(FontPlan.FontsKey)} {Ps(name)} {Ps(file)} $K::String");
            foreach (var sub in FontPlan.SubstituteNames)
                sb.AppendLine($"  DelV 'HKLM' {Ps(FontPlan.FontSubstitutesKey)} {Ps(sub)}");
            sb.AppendLine("  Write-Host 'Системные настройки (шрифт / экран блокировки) возвращены.'");
            sb.AppendLine("} elseif (-not $HklmOnly) {");
            sb.AppendLine("  Write-Host 'Запрашиваю права администратора для системной части...'");
            sb.AppendLine("  Start-Process powershell -Verb RunAs -Wait -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File', \"`\"$PSCommandPath`\"\", '-HklmOnly')");
            sb.AppendLine("}");
            sb.AppendLine("if ($HklmOnly) { exit }");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("if ($HklmOnly) { exit }");
        }

        sb.AppendLine("# --- Пользовательская часть (HKCU) ---");
        foreach (var r in hkcu) EmitRestore(sb, r, "");
        sb.AppendLine();

        sb.AppendLine("Add-Type -Namespace IskraRestore -Name Native -MemberDefinition @'");
        sb.AppendLine("[DllImport(\"user32.dll\", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SystemParametersInfo(uint a, uint b, string c, uint d);");
        sb.AppendLine("[DllImport(\"user32.dll\", EntryPoint = \"SystemParametersInfoW\", SetLastError = true)] public static extern bool SystemParametersInfoInt(uint a, uint b, IntPtr c, uint d);");
        sb.AppendLine("[DllImport(\"user32.dll\", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, UIntPtr w, string l, uint f, uint t, out UIntPtr r);");
        sb.AppendLine("[StructLayout(LayoutKind.Sequential)] public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public int left, top, right, bottom; public IntPtr lParam; }");
        sb.AppendLine("[DllImport(\"shell32.dll\")] public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);");
        sb.AppendLine("[DllImport(\"gdi32.dll\", CharSet = CharSet.Unicode)] public static extern int AddFontResource(string path);");
        sb.AppendLine("'@");
        if (s.Wallpaper != null && s.Wallpaper.Paths.Count > 0 && !string.IsNullOrEmpty(s.Wallpaper.Paths[0]))
        {
            sb.AppendLine($"[IskraRestore.Native]::SystemParametersInfo(0x14, 0, {Ps(s.Wallpaper.Paths[0])}, 3) | Out-Null  # обои");
        }
        if (s.CursorsTouched) sb.AppendLine("[IskraRestore.Native]::SystemParametersInfoInt(0x57, 0, [IntPtr]::Zero, 3) | Out-Null  # курсоры");
        if (s.TaskbarAutoHide is bool ah)
        {
            sb.AppendLine("$abd = New-Object IskraRestore.Native+APPBARDATA; $abd.cbSize = [Runtime.InteropServices.Marshal]::SizeOf($abd)");
            sb.AppendLine($"$abd.lParam = [IntPtr]{(ah ? 1 : 2)}; [IskraRestore.Native]::SHAppBarMessage(10, [ref]$abd) | Out-Null  # автоскрытие панели задач");
        }
        sb.AppendLine("$r = [UIntPtr]::Zero");
        sb.AppendLine("foreach ($area in @('ImmersiveColorSet', 'WindowsThemeElement', 'Environment', 'Fonts', 'WindowMetrics')) { [IskraRestore.Native]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [UIntPtr]::Zero, $area, 2, 3000, [ref]$r) | Out-Null }");
        if (s.LockScreenTouched && !string.IsNullOrEmpty(s.LockScreenBackup))
        {
            sb.AppendLine($"# Экран блокировки: исходная картинка сохранена в {s.LockScreenBackup}");
            sb.AppendLine("Write-Host 'Экран блокировки: откройте Параметры → Персонализация → Экран блокировки, если картинка не вернулась сама.'");
            if (appExePath != null) sb.AppendLine($"if (Test-Path {Ps(appExePath)}) {{ Start-Process {Ps(appExePath)} -ArgumentList '--restore-lockscreen' -Wait }}");
        }
        if (s.Shortcuts.Count > 0)
        {
            sb.AppendLine("# --- Возврат значков ярлыков ---");
            sb.AppendLine("$wsh = New-Object -ComObject WScript.Shell");
            foreach (var b in s.Shortcuts)
            {
                sb.AppendLine($"try {{ if (Test-Path {Ps(b.LnkPath)}) {{ $l = $wsh.CreateShortcut({Ps(b.LnkPath)}); $l.IconLocation = {Ps(string.IsNullOrEmpty(b.OriginalIconLocation) ? ",0" : b.OriginalIconLocation)}; $l.Save() }} }} catch {{ Write-Warning $_ }}");
            }
        }
        if (s.FileIcons.Count > 0)
        {
            sb.AppendLine("# --- Возврат значков .url и папок (desktop.ini) ---");
            foreach (var f in s.FileIcons)
            {
                if (f.Kind == "folder")
                {
                    sb.AppendLine($"try {{ $d = {Ps(f.Path)}; $ini = Join-Path $d 'desktop.ini'; if (Test-Path -LiteralPath $ini) {{ [IO.File]::SetAttributes($ini, 'Normal') }}");
                    if (f.Existed && f.Original != null)
                        sb.AppendLine($"  [IO.File]::WriteAllText($ini, {Ps(f.Original)}, [Text.Encoding]::Unicode); [IO.File]::SetAttributes($ini, 'Hidden, System')");
                    else sb.AppendLine("  if (Test-Path -LiteralPath $ini) { Remove-Item -LiteralPath $ini -Force }");
                    sb.AppendLine($"  [IO.File]::SetAttributes($d, [IO.FileAttributes]{f.Attributes}) }} catch {{ Write-Warning $_ }}");
                }
                else if (f.Kind == "url")
                {
                    sb.AppendLine($"try {{ $u = {Ps(f.Path)}; if (Test-Path -LiteralPath $u) {{ $l = Get-Content -LiteralPath $u | Where-Object {{ $_ -notmatch '^Icon(File|Index)=' }}");
                    if (f.Existed && f.Original != null)
                    {
                        var parts = f.Original.Split('|');
                        sb.AppendLine($"  $l += 'IconFile=' + {Ps(parts[0])}; $l += 'IconIndex=' + {Ps(parts.Length > 1 ? parts[1] : "0")}");
                    }
                    sb.AppendLine("  Set-Content -LiteralPath $u -Value $l } } catch { Write-Warning $_ }");
                }
            }
        }
        if (s.IconSpacingTouched || s.Registry.Any(r => r.Name == "IconSpacing"))
        {
            sb.AppendLine("# --- Сетка рабочего стола вживую (SPI_ICONHORIZONTALSPACING / SPI_ICONVERTICALSPACING) ---");
            sb.AppendLine("$wm = Get-ItemProperty 'HKCU:\\Control Panel\\Desktop\\WindowMetrics'; $dpi = if ($wm.AppliedDPI) { [int]$wm.AppliedDPI } else { 96 }");
            sb.AppendLine("function GridPx($t) { $v = 0; if ([int]::TryParse(\"$t\", [ref]$v) -and $v -lt 0) { [Math]::Min(150, [Math]::Max(43, [int](-$v / 15))) } else { 75 } }");
            sb.AppendLine("[IskraRestore.Native]::SystemParametersInfoInt(0x0D, [uint32]((GridPx $wm.IconSpacing) * $dpi / 96), [IntPtr]::Zero, 3) | Out-Null");
            sb.AppendLine("[IskraRestore.Native]::SystemParametersInfoInt(0x18, [uint32]((GridPx $wm.IconVerticalSpacing) * $dpi / 96), [IntPtr]::Zero, 3) | Out-Null");
        }
        if (s.ShellIconsTouched || s.Shortcuts.Count > 0 || s.FileIcons.Count > 0)
            sb.AppendLine("Add-Type -Namespace IskraIco -Name N -MemberDefinition '[DllImport(\"shell32.dll\")] public static extern void SHChangeNotify(uint e, uint f, System.IntPtr a, System.IntPtr b);'; [IskraIco.N]::SHChangeNotify(0x08000000,0,[IntPtr]::Zero,[IntPtr]::Zero)");
        if (s.ContextMenuTouched)
            sb.AppendLine("Write-Host 'Контекстное меню вернётся после перезапуска Проводника или повторного входа.'");
        if (s.Registry.Any(r => r.Category == "font" || r.Category == "metrics") || s.HklmTouched)
        {
            sb.AppendLine("# Перерегистрация Segoe UI и уведомление о смене шрифта");
            sb.AppendLine("$wf = Join-Path $env:WINDIR 'Fonts'");
            foreach (var (_, file) in FontPlan.SegoeDefaults)
                sb.AppendLine($"$p = Join-Path $wf {Ps(file)}; if (Test-Path $p) {{ [IskraRestore.Native]::AddFontResource($p) | Out-Null }}");
            sb.AppendLine("[IskraRestore.Native]::SendMessageTimeout([IntPtr]0xffff, 0x1D, [UIntPtr]::Zero, '', 2, 3000, [ref]$r) | Out-Null  # WM_FONTCHANGE");
            sb.AppendLine("# Перезапуск Проводника, чтобы рабочий стол и панель задач перечитали шрифты");
            sb.AppendLine("try { Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 1200; if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe } } catch {}");
        }
        sb.AppendLine("Write-Host 'Готово. Если шрифт в части приложений остался прежним — перезапустите их или выйдите из системы и войдите снова.'");
        return sb.ToString();
    }

    private static void EmitRestore(StringBuilder sb, RegOriginal r, string indent)
    {
        string h = r.Hive == RegHive.LocalMachine ? "HKLM" : "HKCU";
        if (r.Subtree != null)
        {
            // Мы удаляли целый ключ — восстанавливаем его значения.
            foreach (var sub in r.Subtree) EmitRestore(sb, sub, indent);
            if (r.Subtree.Count == 0 && r.KeyTreeExisted) sb.AppendLine($"{indent}(RootOf '{h}').CreateSubKey({Ps(r.Key)}).Close()");
            return;
        }
        if (!r.KeyTreeExisted && r.CreatedKeyRoot != null)
        {
            sb.AppendLine($"{indent}DelK '{h}' {Ps(r.CreatedKeyRoot)}");
            return;
        }
        if (!r.ValueExisted)
        {
            sb.AppendLine($"{indent}DelV '{h}' {Ps(r.Key)} {Ps(r.Name)}");
            return;
        }
        string value, kind;
        switch (r.Kind)
        {
            case RegKind.DWord: value = $"([int]{r.NumberValue ?? 0})"; kind = "$K::DWord"; break;
            case RegKind.ExpandString: value = Ps(r.StringValue ?? ""); kind = "$K::ExpandString"; break;
            case RegKind.Binary:
                var bytes = Convert.FromBase64String(r.BinaryBase64 ?? "");
                value = bytes.Length == 0 ? "([byte[]]@())" : "([byte[]](" + string.Join(",", bytes.Select(b => "0x" + b.ToString("X2"))) + "))";
                kind = "$K::Binary"; break;
            case RegKind.MultiString: value = "([string[]]@(" + string.Join(",", (r.MultiValue ?? Array.Empty<string>()).Select(Ps)) + "))"; kind = "$K::MultiString"; break;
            default: value = Ps(r.StringValue ?? ""); kind = "$K::String"; break;
        }
        sb.AppendLine($"{indent}SetV '{h}' {Ps(r.Key)} {Ps(r.Name)} {value} {kind}");
    }

    /// <summary>Строка PowerShell в одинарных кавычках (без интерполяции).</summary>
    public static string Ps(string s) => "'" + s.Replace("'", "''") + "'";
}
