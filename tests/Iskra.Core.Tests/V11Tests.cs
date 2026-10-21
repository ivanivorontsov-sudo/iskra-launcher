using System.Linq;
using Iskra.Core.Settings;
using Iskra.Core.Themes;
using Iskra.Core.Windows;
using Xunit;

namespace Iskra.Core.Tests;

// Тесты для исправлений и возможностей версии 1.1.0.
public class ContrastTests
{
    [Fact]
    public void ReadableKeepsGoodContrast()
    {
        var black = new Argb(0xFF, 0x10, 0x10, 0x10);
        var white = new Argb(0xFF, 0xF4, 0xF4, 0xF6);
        Assert.True(Argb.Contrast(black, white) > 10);
        // Тёмный текст на тёмном фоне должен быть заменён на светлый.
        var fixedFg = Argb.Readable(new Argb(0xFF, 0x20, 0x20, 0x20), black, 4.0);
        Assert.True(Argb.Contrast(fixedFg, black) >= 4.0);
    }

    [Fact]
    public void WhiteOnWhiteGetsFixed()
    {
        var bg = new Argb(0xFF, 0xFF, 0xF5, 0xF7); // почти белый (как у темы «Тетрадь»/«Сакура»)
        var fixedFg = Argb.Readable(new Argb(0xFF, 0xF0, 0xF0, 0xF0), bg, 4.0);
        Assert.True(Argb.Contrast(fixedFg, bg) >= 4.0, "текст не должен сливаться с фоном");
    }

    [Fact]
    public void SemiTransparentForegroundComposited()
    {
        var bg = new Argb(0xFF, 0x20, 0x20, 0x24);
        var dim = new Argb(0xB3, 0xFF, 0xFF, 0xFF);
        var eff = dim.Over(bg);
        Assert.Equal(0xFF, eff.A);
        Assert.True(Argb.Contrast(eff, bg) > 1);
    }
}

public class SelfHealTests
{
    [Fact]
    public void BadThemeColorsAreRepaired()
    {
        var t = new ThemeDefinition { Dark = false };
        t.Palette.Background = "не цвет";
        t.Palette.Foreground = "#zzz";
        t.Fonts.Ui = "";
        t.Shape.TileSize = -5;
        t.Sanitize();
        Assert.True(Argb.TryParse(t.Palette.Background, out _));
        Assert.True(Argb.TryParse(t.Palette.Foreground, out _));
        Assert.False(string.IsNullOrWhiteSpace(t.Fonts.Ui));
        Assert.True(t.Shape.TileSize >= 48);
        // текст контрастен фону
        Assert.True(Argb.Contrast(Argb.Parse(t.Palette.Foreground), Argb.Parse(t.Palette.Background) with { A = 0xFF }) >= 3.0);
    }

    [Fact]
    public void CorruptSettingsFallBackToDefaults()
    {
        var s = AppSettings.FromJson("{ this is : not json ]");
        Assert.NotNull(s);
        Assert.True(s.AllThemes().Any(t => t.Id == s.ThemeId));
    }

    [Fact]
    public void SanitizeClampsAndFixesTheme()
    {
        var s = new AppSettings { SoundVolume = 9, FileSearchMaxDepth = 0, ThemeId = "doesnotexist", Hotkey = "" };
        s.Sanitize();
        Assert.InRange(s.SoundVolume, 0, 1);
        Assert.InRange(s.FileSearchMaxDepth, 1, 12);
        Assert.Equal("Alt+Space", s.Hotkey);
        Assert.True(s.AllThemes().Any(t => t.Id == s.ThemeId));
    }
}

public class SystemPlanV11Tests
{
    private static SystemProfile Dark() => new() { AppsDark = true, SystemDark = true, Accent = "#FF5AA9FF" };

    [Fact]
    public void PlanIncludesClassicColors()
    {
        var plan = SystemPlan.Build(Dark(), new SystemApplyOptions { ClassicColors = true });
        Assert.Contains(plan, w => w.Key == SystemPlan.ColorsKey && w.Name == "WindowText");
        Assert.Contains(plan, w => w.Key == SystemPlan.ColorsKey && w.Name == "Hilight");
    }

    [Fact]
    public void PlanIncludesWindowMetricsWhenSet()
    {
        var p = Dark(); p.ScrollbarWidth = 18; p.TitleBarHeight = 30;
        var plan = SystemPlan.Build(p, new SystemApplyOptions { WindowMetrics = true });
        Assert.Contains(plan, w => w.Key == SystemPlan.WindowMetricsKey && w.Name == "ScrollWidth");
        Assert.Contains(plan, w => w.Key == SystemPlan.WindowMetricsKey && w.Name == "CaptionHeight");
    }

    [Fact]
    public void PlanIncludesShellIconsWhenAssetsProvided()
    {
        var assets = new SystemAssets();
        assets.IconFiles["folder"] = @"C:\x\folder.ico";
        assets.IconFiles["recycleEmpty"] = @"C:\x\re.ico";
        var plan = SystemPlan.Build(Dark(), new SystemApplyOptions { ShellIcons = true }, assets);
        Assert.Contains(plan, w => w.Key == SystemPlan.ShellIconsKey && w.Name == "3");
        Assert.Contains(plan, w => w.Key == SystemPlan.FolderIconKey);
        Assert.Contains(plan, w => w.Key == SystemPlan.DirectoryIconKey);
        Assert.Contains(plan, w => w.Key == SystemPlan.RecycleIconKey && w.Name == "empty");
    }

    [Fact]
    public void EveryPlanWriteIsAllowedByGuard()
    {
        var p = Dark(); p.ScrollbarWidth = 18; p.TitleBarHeight = 30;
        var assets = new SystemAssets();
        assets.IconFiles["folder"] = @"C:\x\folder.ico";
        assets.IconFiles["thispc"] = @"C:\x\pc.ico";
        assets.IconFiles["recycleEmpty"] = @"C:\x\re.ico";
        assets.IconFiles["recycleFull"] = @"C:\x\rf.ico";
        var plan = SystemPlan.Build(p, new SystemApplyOptions { ShellIcons = true, WindowMetrics = true }, assets);
        foreach (var w in plan) Assert.True(RegistryGuard.IsAllowed(w), $"охранник должен разрешать {w}");
    }
}

public class GuardV11Tests
{
    [Fact]
    public void AllowsNewThemingKeys()
    {
        Assert.True(RegistryGuard.IsAllowed(RegHive.CurrentUser, SystemPlan.ColorsKey, "WindowText"));
        Assert.True(RegistryGuard.IsAllowed(RegHive.CurrentUser, SystemPlan.ShellIconsKey, "3"));
        Assert.True(RegistryGuard.IsAllowed(RegHive.CurrentUser, SystemPlan.RecycleIconKey, "empty"));
        Assert.True(RegistryGuard.IsAllowed(RegHive.CurrentUser, SystemPlan.ThisPcIconKey, ""));
    }

    [Fact]
    public void StillBlocksStopScreenAndPolicies()
    {
        Assert.False(RegistryGuard.IsAllowed(RegHive.CurrentUser, @"Software\Policies\Microsoft\Windows\System", "DisableRegistryTools"));
        Assert.False(RegistryGuard.IsAllowed(RegHive.LocalMachine, @"SOFTWARE\StopScreen", "NoDelete"));
        Assert.False(RegistryGuard.IsAllowed(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr"));
        Assert.False(RegistryGuard.IsAllowed(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\UCPD", "Start"));
        // произвольный CLSID DefaultIcon (не из белого списка) — нельзя
        Assert.False(RegistryGuard.IsAllowed(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{00000000-0000-0000-0000-000000000000}\DefaultIcon", ""));
    }
}

public class IconShapeTests
{
    [Fact]
    public void EveryBuiltInThemeHasIconStyle()
    {
        Assert.All(BuiltInThemes.All, t => Assert.NotNull(t.Icon));
        // Разные формы у разных тем.
        Assert.True(BuiltInThemes.All.Select(t => t.Icon.Shape).Distinct().Count() >= 5, "темы должны отличаться формой значков");
    }

    [Fact]
    public void IconStyleSurvivesJsonRoundTrip()
    {
        var t = BuiltInThemes.Get("sakura").Clone();
        t.Icon.Shape = IconShape.Droplet;
        var round = ThemeDefinition.FromJson(t.ToJson());
        Assert.NotNull(round);
        Assert.Equal(IconShape.Droplet, round!.Icon.Shape);
    }
}
