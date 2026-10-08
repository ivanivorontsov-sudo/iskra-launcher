using System.Collections.Generic;
using System.Linq;
using Iskra.Core.Settings;
using Iskra.Core.Windows;
using Xunit;

namespace Iskra.Core.Tests;

// Тесты шрифтовой части 1.1.0: починка Segoe UI, подстановка без обнуления, LOGFONT, снимок/скрипт отката.
public class FontRepairTests
{
    [Fact]
    public void RepairSetsAllSegoeDefaultsAndDeletesSubstitutes()
    {
        var plan = FontPlan.BuildFontRepair();
        // Все штатные записи семейства должны прописываться строкой (а не обнуляться).
        foreach (var (name, file) in FontPlan.SegoeDefaults)
        {
            var w = plan.Single(x => x.Hive == RegHive.LocalMachine && x.Key == FontPlan.FontsKey && x.Name == name);
            Assert.Equal(RegKind.String, w.Kind);
            Assert.Equal(file, w.Value);
            Assert.False(string.IsNullOrEmpty((string)w.Value!));
        }
        // Подстановки базового шрифта должны удаляться.
        foreach (var sub in FontPlan.SubstituteNames)
        {
            var w = plan.Single(x => x.Key == FontPlan.FontSubstitutesKey && x.Name == sub);
            Assert.Equal(RegKind.DeleteValue, w.Kind);
        }
    }

    [Fact]
    public void SubstitutePlanNeverBlanksSegoeEntries()
    {
        var plan = FontPlan.BuildSystemFontSubstitute("Caveat", "Caveat-400.ttf");
        // Старый баг: обнуление «Segoe UI (TrueType)». Такого быть не должно.
        Assert.DoesNotContain(plan, w => w.Key == FontPlan.FontsKey
            && w.Name.StartsWith("Segoe UI") && (w.Value as string) == "");
        // Должна появиться подстановка Segoe UI -> Caveat и запись файла семейства.
        Assert.Contains(plan, w => w.Key == FontPlan.FontSubstitutesKey && w.Name == "Segoe UI" && (string)w.Value! == "Caveat");
        Assert.Contains(plan, w => w.Key == FontPlan.FontsKey && w.Name == "Caveat (TrueType)" && (string)w.Value! == "Caveat-400.ttf");
    }

    [Theory]
    [InlineData("Segoe UI", null, false, true)]       // есть подстановка -> сломано
    [InlineData(null, "", true, true)]                // обнулённая запись -> сломано
    [InlineData(null, "segoeui.ttf", false, false)]   // всё штатно -> не сломано
    public void LooksBrokenDetectsTrouble(string? substitute, string? segoeUiFile, bool blankOne, bool expected)
    {
        var fonts = new Dictionary<string, string?>();
        foreach (var n in FontPlan.SegoeNamesThatMayBeBlanked) fonts[n] = "ok.ttf";
        if (segoeUiFile != null) fonts["Segoe UI (TrueType)"] = segoeUiFile;
        if (blankOne) fonts["Segoe UI (TrueType)"] = "";
        Assert.Equal(expected, FontPlan.LooksBroken(substitute, fonts));
    }
}

public class LogFontTests
{
    [Fact]
    public void FaceNameRoundTrips()
    {
        var lf = FontPlan.BuildLogFont("Caveat", 11, 700, italic: true);
        Assert.Equal(FontPlan.LogFontSize, lf.Length);
        Assert.Equal("Caveat", FontPlan.ReadFaceName(lf));
    }

    [Fact]
    public void WithFaceNamePreservesOtherFields()
    {
        var orig = FontPlan.BuildLogFont("Segoe UI", 9, 400);
        var changed = FontPlan.WithFaceName(orig, "Comfortaa");
        Assert.Equal("Comfortaa", FontPlan.ReadFaceName(changed));
        // Первые 28 байт (высота/вес и пр.) не должны измениться.
        for (int i = 0; i < 28; i++) Assert.Equal(orig[i], changed[i]);
    }

    [Fact]
    public void LongFaceNameDoesNotOverflow()
    {
        var lf = FontPlan.WithFaceName(new byte[FontPlan.LogFontSize], new string('X', 100));
        Assert.Equal(FontPlan.LogFontSize, lf.Length);
    }
}

public class FontSnapshotTests
{
    [Fact]
    public void BinaryLogFontSurvivesSnapshotRoundTrip()
    {
        var blob = FontPlan.BuildLogFont("Segoe UI", 9, 400);
        var snap = new SystemSnapshot();
        snap.Registry.Add(new RegOriginal
        {
            Hive = RegHive.CurrentUser, Key = FontPlan.WindowMetricsKey, Name = "CaptionFont",
            Category = "metrics", ValueExisted = true, Kind = RegKind.Binary,
            BinaryBase64 = System.Convert.ToBase64String(blob),
        });
        var back = SystemSnapshot.FromJson(snap.ToJson())!;
        var r = back.Registry.Single();
        Assert.Equal(RegKind.Binary, r.Kind);
        var restored = System.Convert.FromBase64String(r.BinaryBase64!);
        Assert.Equal("Segoe UI", FontPlan.ReadFaceName(restored));
    }

    [Fact]
    public void RestoreScriptIncludesFontRepairAndExplorerRestart()
    {
        var snap = new SystemSnapshot { HklmTouched = true };
        snap.Registry.Add(new RegOriginal
        {
            Hive = RegHive.LocalMachine, Key = FontPlan.FontSubstitutesKey, Name = "Segoe UI",
            Category = "font", ValueExisted = false, // раньше записи не было -> при откате удаляем
        });
        var script = RestoreScriptGenerator.Generate(snap, null);
        Assert.Contains("segoeui.ttf", script);                 // возврат штатной регистрации
        Assert.Contains("FontSubstitutes", script);
        Assert.Contains("0x1D", script);                        // WM_FONTCHANGE
        Assert.Contains("Stop-Process -Name explorer", script); // перезапуск Проводника
        Assert.Contains("AddFontResource", script);             // перерегистрация шрифтов
    }
}

public class FontGuardTests
{
    [Fact]
    public void LogFontMetricNamesAreAllowed()
    {
        foreach (var n in new[] { "CaptionFont", "SmCaptionFont", "MenuFont", "MessageFont", "StatusFont", "IconFont" })
            Assert.True(RegistryGuard.IsAllowed(RegHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", n), n);
    }

    [Fact]
    public void FontHivesAllowedButPoliciesBlocked()
    {
        Assert.True(RegistryGuard.IsAllowed(RegHive.LocalMachine, FontPlan.FontSubstitutesKey, "Segoe UI"));
        Assert.True(RegistryGuard.IsAllowed(RegHive.LocalMachine, FontPlan.FontsKey, "Segoe UI (TrueType)"));
        Assert.False(RegistryGuard.IsAllowed(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr"));
    }

    [Fact]
    public void EveryFontPlanWriteIsAllowedByGuard()
    {
        foreach (var w in FontPlan.BuildFontRepair()) Assert.True(RegistryGuard.IsAllowed(w), w.ToString());
        foreach (var w in FontPlan.BuildSystemFontSubstitute("Caveat", "Caveat-400.ttf")) Assert.True(RegistryGuard.IsAllowed(w), w.ToString());
    }
}

public class FontSettingsTests
{
    [Fact]
    public void SanitizeClampsSizeAndCleansNames()
    {
        var f = new FontSettings { SizePt = 999, TitleFont = "  Caveat  ", MenuFont = "   " };
        f.Sanitize();
        Assert.True(f.SizePt <= 18);
        Assert.Equal("Caveat", f.TitleFont);
        Assert.Null(f.MenuFont);
    }

    [Fact]
    public void FontsSurviveSettingsRoundTrip()
    {
        var s = new AppSettings();
        s.Fonts.Enabled = true; s.Fonts.TitleFont = "Comfortaa"; s.Fonts.ReplaceSystemWide = true; s.Fonts.SystemFont = "Caveat";
        var back = AppSettings.FromJson(s.ToJson());
        Assert.True(back.Fonts.Enabled);
        Assert.Equal("Comfortaa", back.Fonts.TitleFont);
        Assert.Equal("Caveat", back.Fonts.SystemFont);
    }
}
