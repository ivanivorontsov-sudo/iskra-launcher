using Iskra.Core.Media;
using Iskra.Core.Settings;
using Iskra.Core.Themes;
using Iskra.Core.Windows;
using Xunit;

namespace Iskra.Core.Tests;

public class ThemeTests
{
    [Fact]
    public void AtLeastTenDistinctThemes()
    {
        var all = BuiltInThemes.All;
        Assert.True(all.Count >= 10);
        Assert.Equal(all.Count, all.Select(t => t.Id).Distinct().Count());
        Assert.True(all.Select(t => t.Fonts.Ui + "/" + t.Fonts.Title).Distinct().Count() >= 9, "темы должны реально менять шрифты");
        Assert.True(all.Select(t => t.Effects.Open).Distinct().Count() >= 7, "разные анимации");
        Assert.True(all.Select(t => t.System.Wallpaper).Distinct().Count() == all.Count, "свои обои у каждой");
        Assert.All(all, t => Assert.True(t.BuiltIn));
    }

    [Fact]
    public void HandwrittenCyrillicThemesExist()
    {
        var hand = new[] { "Caveat", "Marck Script", "Neucha", "Amatic SC" };
        Assert.Contains(BuiltInThemes.All, t => hand.Contains(t.Fonts.Ui) || hand.Contains(t.Fonts.Title));
    }

    [Fact]
    public void ColorsParse()
    {
        foreach (var t in BuiltInThemes.All)
            foreach (var p in typeof(ThemePalette).GetProperties().Where(p => p.PropertyType == typeof(string)))
                Assert.True(Argb.TryParse((string)p.GetValue(t.Palette)!, out _), $"{t.Id}.{p.Name}");
    }

    [Fact]
    public void JsonRoundTrip()
    {
        var t = BuiltInThemes.Get("neon").Clone();
        t.Id = "my"; t.Name = "Моя";
        var back = ThemeDefinition.FromJson(t.ToJson())!;
        Assert.Equal("Моя", back.Name);
        Assert.Equal(OpenAnimation.Glitch, back.Effects.Open);
        Assert.Contains("\"Glitch\"", t.ToJson());
    }

    [Fact]
    public void NoBrandNamesInThemeNames()
    {
        var banned = new[] { "windows", "microsoft", "apple", "mac", "iphone", "metro", "spotlight", "segoe", "raycast" };
        foreach (var t in BuiltInThemes.All)
            foreach (var b in banned)
            {
                Assert.DoesNotContain(b, t.Name.ToLowerInvariant());
                Assert.DoesNotContain(b, t.Description.ToLowerInvariant());
            }
    }
}

public class SystemPlanTests
{
    [Fact]
    public void AllPlannedWritesPassGuard()
    {
        var assets = new SystemAssets();
        foreach (var r in SystemPlan.CursorRoles) assets.Cursors[r] = @"C:\x\" + r + ".cur";
        foreach (var e in SystemPlan.SoundEvents) assets.Sounds[e] = @"C:\x\" + e + ".wav";
        foreach (var t in BuiltInThemes.All)
        {
            var plan = SystemPlan.Build(t.System, new SystemApplyOptions(), assets);
            Assert.NotEmpty(plan);
            Assert.All(plan, w => Assert.True(RegistryGuard.IsAllowed(w), w.ToString()));
        }
        Assert.All(SystemPlan.BuildSystemFont("Caveat", "Caveat.ttf"), w => Assert.True(RegistryGuard.IsAllowed(w), w.ToString()));
        Assert.All(SystemPlan.BuildLockScreenCsp(@"C:\a.png"), w => Assert.True(RegistryGuard.IsAllowed(w), w.ToString()));
    }

    [Theory]
    [InlineData(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr")]
    [InlineData(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableRegistryTools")]
    [InlineData(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDelete")]
    [InlineData(RegHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "DisableCMD")]
    [InlineData(RegHive.LocalMachine, @"SOFTWARE\StopScreen", "Anything")]
    [InlineData(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\StopScreen", "Start")]
    [InlineData(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Iskra")]
    [InlineData(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa")]
    [InlineData(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden")]
    [InlineData(RegHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Shell")]
    [InlineData(RegHive.CurrentUser, @"Control Panel\Desktop", "ScreenSaveActive")]
    [InlineData(RegHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme")]
    public void GuardBlocksDangerousKeys(RegHive hive, string key, string name) => Assert.False(RegistryGuard.IsAllowed(hive, key, name));

    [Fact]
    public void ColorsAreEncodedForWindows()
    {
        var p = new SystemProfile { Accent = "#FF112233", AccentOnTaskbar = true, SystemDark = true };
        var plan = SystemPlan.Build(p, new SystemApplyOptions());
        var accent = plan.Single(w => w.Key == SystemPlan.DwmKey && w.Name == "AccentColor");
        Assert.Equal(unchecked((int)0xFF332211), accent.Value);
        var pal = (byte[])plan.Single(w => w.Name == "AccentPalette").Value!;
        Assert.Equal(32, pal.Length);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0 }, pal[12..16]);
        Assert.Equal(1, plan.Single(w => w.Key == SystemPlan.PersonalizeKey && w.Name == "ColorPrevalence").Value);
        // В светлом системном режиме акцент на панели задач Windows не поддерживает.
        var light = SystemPlan.Build(new SystemProfile { AccentOnTaskbar = true, SystemDark = false }, new SystemApplyOptions());
        Assert.Equal(0, light.Single(w => w.Key == SystemPlan.PersonalizeKey && w.Name == "ColorPrevalence").Value);
    }

    [Fact]
    public void OptionsDisableCategories()
    {
        var o = new SystemApplyOptions { Colors = false, ContextMenu = false, Explorer = false, Taskbar = false, IconSpacing = false };
        Assert.Empty(SystemPlan.Build(BuiltInThemes.Get("terminal").System, o));
    }

    [Fact]
    public void ClassicContextMenuToggle()
    {
        var on = SystemPlan.Build(new SystemProfile { ClassicContextMenu = true }, new SystemApplyOptions());
        Assert.Contains(on, w => w.Key == SystemPlan.ClassicMenuKey && w.Kind == RegKind.String && (string)w.Value! == "");
        var off = SystemPlan.Build(new SystemProfile { ClassicContextMenu = false }, new SystemApplyOptions());
        Assert.Contains(off, w => w.Key == SystemPlan.ClassicMenuClsid && w.Kind == RegKind.DeleteKey);
    }
}

public class SnapshotTests
{
    [Fact]
    public void RestoreScriptCoversEveryEntry()
    {
        var s = new SystemSnapshot();
        s.Registry.Add(new RegOriginal { Hive = RegHive.CurrentUser, Key = SystemPlan.PersonalizeKey, Name = "AppsUseLightTheme", ValueExisted = true, Kind = RegKind.DWord, NumberValue = 1 });
        s.Registry.Add(new RegOriginal { Hive = RegHive.CurrentUser, Key = SystemPlan.CursorsKey, Name = "Arrow", ValueExisted = true, Kind = RegKind.ExpandString, StringValue = @"%SystemRoot%\cursors\aero_arrow.cur" });
        s.Registry.Add(new RegOriginal { Hive = RegHive.CurrentUser, Key = SystemPlan.DwmKey, Name = "AccentColor", ValueExisted = false, Kind = RegKind.DWord });
        s.Registry.Add(new RegOriginal { Hive = RegHive.CurrentUser, Key = SystemPlan.ClassicMenuKey, Name = "", ValueExisted = false, KeyTreeExisted = false, CreatedKeyRoot = SystemPlan.ClassicMenuClsid });
        s.Registry.Add(new RegOriginal { Hive = RegHive.CurrentUser, Key = SystemPlan.AccentKey, Name = "AccentPalette", ValueExisted = true, Kind = RegKind.Binary, BinaryBase64 = Convert.ToBase64String(new byte[] { 1, 2, 255 }) });
        s.Registry.Add(new RegOriginal { Hive = RegHive.LocalMachine, Key = SystemPlan.FontSubstitutesKey, Name = "Segoe UI", ValueExisted = false });
        s.Registry.Add(new RegOriginal { Hive = RegHive.CurrentUser, Key = @"Control Panel\Desktop", Name = "WallPaper", ValueExisted = true, Kind = RegKind.String, StringValue = @"C:\Users\Иван\it's.jpg" });
        s.CursorsTouched = true; s.TaskbarAutoHide = false;
        s.Wallpaper = new WallpaperOriginal { Paths = { @"C:\Users\Иван\it's.jpg" } };
        var ps = RestoreScriptGenerator.Generate(s);
        Assert.Contains("SetV 'HKCU' 'Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' 'AppsUseLightTheme' ([int]1) $K::DWord", ps);
        Assert.Contains("$K::ExpandString", ps);
        Assert.Contains("DelV 'HKCU' 'Software\\Microsoft\\Windows\\DWM' 'AccentColor'", ps);
        Assert.Contains("DelK 'HKCU' 'Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}'", ps);
        Assert.Contains("([byte[]](0x01,0x02,0xFF))", ps);
        Assert.Contains("DelV 'HKLM'", ps);
        Assert.Contains("-Verb RunAs", ps);
        Assert.Contains("it''s.jpg", ps); // экранирование кавычек
        Assert.Contains("0x57", ps);
        Assert.DoesNotContain("reg.exe", ps);
        Assert.DoesNotContain("regedit", ps);
        Assert.DoesNotContain("cmd ", ps);
    }

    [Fact]
    public void SnapshotJsonRoundTrip()
    {
        var s = new SystemSnapshot { TaskbarAutoHide = true };
        s.Registry.Add(new RegOriginal { Hive = RegHive.CurrentUser, Key = "A", Name = "B", ValueExisted = true, Kind = RegKind.DWord, NumberValue = -5 });
        var back = SystemSnapshot.FromJson(s.ToJson())!;
        Assert.True(back.Contains(RegHive.CurrentUser, "a", "b"));
        Assert.Equal(-5, back.Registry[0].NumberValue);
        Assert.True(back.TaskbarAutoHide);
    }
}

public class MediaTests
{
    [Fact]
    public void WavHeaderIsValid()
    {
        foreach (SoundPack p in Enum.GetValues<SoundPack>())
        {
            var wav = SoundSynth.Wav(p, SoundEvent.Notify);
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
            Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
            Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
            Assert.True(wav.Length < 400_000, $"{p}: {wav.Length}");
        }
    }

    [Fact]
    public void SoundsDifferBetweenPacks()
    {
        var a = SoundSynth.Render(SoundPack.Glass, SoundEvent.Open);
        var b = SoundSynth.Render(SoundPack.Chip, SoundEvent.Open);
        Assert.NotEqual(a.Length, b.Length == a.Length ? -1 : b.Length);
        Assert.All(a, s => Assert.InRange(s, -1f, 1f));
    }

    [Fact]
    public void CurFileStructure()
    {
        var px = new byte[32 * 32 * 4];
        for (int i = 3; i < px.Length; i += 4) px[i] = (byte)(i % 8 == 3 ? 255 : 0);
        var cur = CursorWriter.WriteCur(new[] { new CursorImage(32, px, 1, 2), new CursorImage(48, new byte[48 * 48 * 4], 2, 3) });
        Assert.Equal(0, BitConverter.ToInt16(cur, 0));
        Assert.Equal(2, BitConverter.ToInt16(cur, 2)); // тип 2 = курсор
        Assert.Equal(2, BitConverter.ToInt16(cur, 4));
        Assert.Equal(32, cur[6]);
        Assert.Equal(1, BitConverter.ToInt16(cur, 10)); // hotspot X
        Assert.Equal(2, BitConverter.ToInt16(cur, 12));
        int off = BitConverter.ToInt32(cur, 18);
        Assert.Equal(40, BitConverter.ToInt32(cur, off)); // BITMAPINFOHEADER
        Assert.Equal(64, BitConverter.ToInt32(cur, off + 8)); // высота ×2
        var ani = CursorWriter.WriteAni(new[] { cur, cur }, 4);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(ani, 0, 4));
        Assert.Equal("ACON", System.Text.Encoding.ASCII.GetString(ani, 8, 4));
        Assert.Equal(ani.Length - 8, BitConverter.ToInt32(ani, 4));
    }
}

public class HotkeyAndSettingsTests
{
    [Theory]
    [InlineData("Alt+Space", "Alt+Space")]
    [InlineData("alt + space", "Alt+Space")]
    [InlineData("Shift+Ctrl+k", "Ctrl+Shift+K")]
    [InlineData("Win+`", "Win+`")]
    [InlineData("F12", "F12")]
    [InlineData("Ctrl+Alt+5", "Ctrl+Alt+5")]
    public void ParseAndFormat(string input, string expected)
    {
        Assert.True(Hotkey.TryParse(input, out var hk));
        Assert.Equal(expected, hk.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("K")]
    [InlineData("Alt+K+L")]
    [InlineData("Ctrl+Alt")]
    public void RejectsBad(string input) => Assert.False(Hotkey.TryParse(input, out _));

    [Fact]
    public void KnownConflicts()
    {
        Hotkey.TryParse("Win+L", out var a);
        Assert.NotNull(Hotkey.KnownSystemConflict(a));
        Hotkey.TryParse("Alt+Space", out var b);
        Assert.Null(Hotkey.KnownSystemConflict(b));
    }

    [Fact]
    public void SettingsRoundTripAndDefaults()
    {
        var s = new AppSettings();
        Assert.Equal("Alt+Space", s.Hotkey);
        Assert.False(s.SystemOptions.SystemFontExperimental); // эксперимент выключен по умолчанию
        s.Pinned.Add(new PinnedItem { Id = "a", Title = "Тест" });
        s.WebSearchEngine = "google";
        var back = AppSettings.FromJson(s.ToJson());
        Assert.Equal("Тест", back.Pinned[0].Title);
        Assert.StartsWith("https://www.google.com", back.WebSearchUrl);
        Assert.Equal("mica", AppSettings.FromJson("{broken").ThemeId);
    }

    [Fact]
    public void LocalizationHasBothLanguages()
    {
        foreach (var (k, v) in Iskra.Core.Loc.L.Strings)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Ru), k);
            Assert.False(string.IsNullOrWhiteSpace(v.En), k);
        }
    }
}
