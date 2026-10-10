using System.Linq;
using Iskra.Core.Settings;
using Iskra.Core.Themes;
using Iskra.Core.Windows;
using Xunit;

namespace Iskra.Core.Tests;

// 1.2.0: сетка рабочего стола, контраст классических цветов, стрелки ярлыков, откат значков.
public class DesktopGridTests
{
    [Theory]
    [InlineData(IconSpacing.Compact, -1020)]
    [InlineData(IconSpacing.Normal, -1125)]
    [InlineData(IconSpacing.Wide, -1260)]
    public void TwipsFormulaIsMinus15TimesPx(IconSpacing s, int expected) => Assert.Equal(expected, SystemPlan.IconSpacingTwips(s));

    [Fact]
    public void GridNeverLeavesSaneRange()
    {
        Assert.Equal(-15 * SystemPlan.GridMaxPx, SystemPlan.PxToTwips(10000));
        Assert.Equal(-15 * SystemPlan.GridMinPx, SystemPlan.PxToTwips(1));
        foreach (var s in new[] { IconSpacing.Compact, IconSpacing.Normal, IconSpacing.Wide })
        {
            var (h, v) = SystemPlan.IconSpacingPx(s)!.Value;
            Assert.InRange(h, 60, 90); Assert.InRange(v, 60, 90); // 1.1.0 давала 100 px → огромная сетка
        }
    }

    [Theory]
    [InlineData("-1125", false)]
    [InlineData("-1128", false)]
    [InlineData("-3000", true)]
    [InlineData("-100", true)]
    [InlineData("abc", true)]
    [InlineData(null, false)]
    public void DetectsBrokenGrid(string? v, bool broken) => Assert.Equal(broken, SystemPlan.IsGridValueBroken(v));

    [Fact]
    public void DensityOverridesTheme()
    {
        var p = BuiltInThemes.All.First(t => t.System.IconSpacing == IconSpacing.Wide).System;
        var plan = SystemPlan.Build(p, new SystemApplyOptions { GridDensity = IconSpacing.Normal });
        Assert.Contains(plan, w => w.Name == "IconSpacing" && (string)w.Value! == "-1125");
        Assert.Contains(plan, w => w.Name == "IconVerticalSpacing" && (string)w.Value! == "-1125");
    }
}

public class ClassicContrastTests
{
    private static Argb C(System.Collections.Generic.List<RegWrite> plan, string n)
    {
        var parts = ((string)plan.Single(w => w.Key == SystemPlan.ColorsKey && w.Name == n).Value!).Split(' ').Select(byte.Parse).ToArray();
        return new Argb(0xFF, parts[0], parts[1], parts[2]);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void EveryClassicPairIsReadable(string id)
    {
        var plan = SystemPlan.Build(BuiltInThemes.Get(id).System, new SystemApplyOptions());
        Assert.True(Argb.Contrast(C(plan, "WindowText"), C(plan, "Window")) >= 7, "WindowText");
        Assert.True(Argb.Contrast(C(plan, "MenuText"), C(plan, "Menu")) >= 7, "MenuText");
        Assert.True(Argb.Contrast(C(plan, "ButtonText"), C(plan, "ButtonFace")) >= 7, "ButtonText");
        Assert.True(Argb.Contrast(C(plan, "HilightText"), C(plan, "Hilight")) >= 4.5, "HilightText");
        Assert.True(Argb.Contrast(C(plan, "TitleText"), C(plan, "ActiveTitle")) >= 4.5, "TitleText");
        Assert.True(Argb.Contrast(C(plan, "GrayText"), C(plan, "Window")) >= 3, "GrayText");
    }

    [Fact]
    public void ClassicColoursFollowAppsMode()
    {
        // Светлый режим приложений + тёмные классические цвета давали «засветлённый» Проводник.
        foreach (var t in BuiltInThemes.All)
        {
            var plan = SystemPlan.Build(t.System, new SystemApplyOptions());
            bool lightWindow = Argb.Contrast(C(plan, "Window"), new Argb(0xFF, 0, 0, 0)) > 10;
            Assert.Equal(!t.System.AppsDark, lightWindow);
        }
    }

    public static System.Collections.Generic.IEnumerable<object[]> Themes() => BuiltInThemes.All.Select(t => new object[] { t.Id });
}

public class ShortcutArrowTests
{
    [Fact]
    public void ArrowOverlayUsesShellIcons29AndIsGuarded()
    {
        var assets = new SystemAssets { BlankIcon = @"C:\x\blank.ico" };
        var plan = SystemPlan.Build(BuiltInThemes.All[0].System, new SystemApplyOptions { HideShortcutArrows = true }, assets);
        var w = plan.Single(x => x.Name == "29");
        Assert.Equal(@"C:\x\blank.ico,0", w.Value);
        Assert.Equal(RegHive.LocalMachine, w.Hive);            // HKCU Windows для оверлея не читает
        Assert.Equal(SystemPlan.ShellIconsKeyLm, w.Key);
        Assert.False(RegistryGuard.IsAllowed(new RegWrite(RegHive.LocalMachine, SystemPlan.ShellIconsKeyLm, "3", RegKind.String, "x", "t")));
        Assert.True(RegistryGuard.IsAllowed(w));
    }

    [Fact]
    public void NoArrowChangeUnlessAsked()
    {
        var plan = SystemPlan.Build(BuiltInThemes.All[0].System, new SystemApplyOptions(), new SystemAssets { BlankIcon = "b.ico" });
        Assert.DoesNotContain(plan, x => x.Name == "29");
    }
}

public class DesktopRestoreScriptTests
{
    [Fact]
    public void ScriptRestoresFoldersUrlsAndGrid()
    {
        var s = new SystemSnapshot { IconSpacingTouched = true };
        s.FileIcons.Add(new FileIconBackup { Path = @"C:\Users\I\Desktop\Files", Kind = "folder", Existed = false, Attributes = 16 });
        s.FileIcons.Add(new FileIconBackup { Path = @"C:\Users\I\Desktop\Site.url", Kind = "url", Existed = true, Original = @"C:\a.ico|3" });
        var script = RestoreScriptGenerator.Generate(s, null);
        Assert.Contains("desktop.ini", script);
        Assert.Contains("Remove-Item", script);           // desktop.ini не было — удаляем
        Assert.Contains("IconFile=", script);
        Assert.Contains("0x0D", script);                  // SPI_ICONHORIZONTALSPACING
        Assert.Contains("0x18", script);                  // SPI_ICONVERTICALSPACING
        Assert.Contains("SHChangeNotify", script);
        Assert.False(s.IsEmpty);
    }

    [Fact]
    public void FileIconsSurviveRoundTrip()
    {
        var s = new SystemSnapshot();
        s.FileIcons.Add(new FileIconBackup { Path = "p", Kind = "folder", Existed = true, Original = "[.ShellClassInfo]", Attributes = 17 });
        var back = SystemSnapshot.FromJson(s.ToJson())!;
        Assert.Equal(17, back.FileIcons.Single().Attributes);
        Assert.Equal("[.ShellClassInfo]", back.FileIcons.Single().Original);
    }

    [Fact]
    public void DesktopSettingsDefaultsAreSafe()
    {
        var a = AppSettings.FromJson("{}");
        Assert.Equal(IconSpacing.Normal, a.DesktopGrid);
        Assert.True(a.HideShortcutArrows);
    }
}
