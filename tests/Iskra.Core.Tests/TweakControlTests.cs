using Iskra.Core.Themes;
using Iskra.Core.Windows;
using Xunit;

namespace Iskra.Core.Tests;

public class TweakControlTests
{
    [Fact]
    public void ClassicColors_AreOffByDefault_AndHaveOwnCategory()
    {
        var p = new SystemProfile();
        var w = SystemPlan.Build(p, new SystemApplyOptions());
        Assert.DoesNotContain(w, x => x.Key == SystemPlan.ColorsKey);
        var w2 = SystemPlan.Build(p, new SystemApplyOptions { ClassicColors = true });
        Assert.All(w2.Where(x => x.Key == SystemPlan.ColorsKey), x => Assert.Equal("classiccolors", x.Category));
    }

    [Fact]
    public void DisabledCategories_FollowOptions()
    {
        var d = TweakControl.DisabledCategories(new SystemApplyOptions { Cursors = false, Taskbar = false });
        Assert.Contains("cursors", d); Assert.Contains("taskbar", d); Assert.Contains("arrow", d);
        Assert.DoesNotContain("colors", d);
    }

    [Fact]
    public void Take_RemovesOnlyThatCategory_IncludingLegacyClassicColors()
    {
        var s = new SystemSnapshot();
        s.Registry.Add(new RegOriginal { Key = SystemPlan.ColorsKey, Name = "Window", Category = "colors" });
        s.Registry.Add(new RegOriginal { Key = SystemPlan.DwmKey, Name = "AccentColor", Category = "colors" });
        s.Registry.Add(new RegOriginal { Hive = RegHive.LocalMachine, Key = SystemPlan.ShellIconsKeyLm, Name = "29", Category = "shellicons" });
        var t = TweakControl.Take(s, new[] { "classiccolors", "arrow" });
        Assert.Equal(2, t.Count);
        Assert.Single(s.Registry);
        Assert.Equal("AccentColor", s.Registry[0].Name);
    }

    [Fact]
    public void ToRestoreWrite_DeletesValuesThatDidNotExist()
    {
        var w = TweakControl.ToRestoreWrite(new RegOriginal { Key = "K", Name = "N", ValueExisted = false });
        Assert.Equal(RegKind.DeleteValue, w.Kind);
        var w2 = TweakControl.ToRestoreWrite(new RegOriginal { Key = "K", Name = "N", ValueExisted = true, Kind = RegKind.DWord, NumberValue = 7 });
        Assert.Equal(7, w2.Value);
    }

    [Fact]
    public void DetectLocks_FindsPolicyValues()
    {
        object? Reader(RegHive h, string k, string n) => n == "NoChangingWallPaper" && h == RegHive.CurrentUser ? 1 : n == "NoChangingLockScreen" ? 0 : null;
        var locks = TweakControl.DetectLocks(Reader);
        Assert.Single(locks);
        Assert.Equal("wallpaper", locks[0].Category);
    }

    [Fact]
    public void VerifyWrites_ReportsValuesThatDidNotStick()
    {
        var writes = new[]
        {
            new RegWrite(RegHive.CurrentUser, "A", "x", RegKind.DWord, 1, "colors"),
            new RegWrite(RegHive.CurrentUser, "A", "y", RegKind.String, "v", "taskbar"),
        };
        var bad = TweakControl.VerifyWrites(writes, (h, k, n) => n == "x" ? 1 : "other");
        Assert.Equal(new[] { "taskbar" }, bad);
    }

    [Fact]
    public void ArrowWrite_UsesOwnCategory()
    {
        var w = SystemPlan.Build(new SystemProfile(), new SystemApplyOptions { HideShortcutArrows = true }, new SystemAssets { BlankIcon = @"C:\b.ico" });
        Assert.Contains(w, x => x.Hive == RegHive.LocalMachine && x.Category == "arrow");
    }
}
