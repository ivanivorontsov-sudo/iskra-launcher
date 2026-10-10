using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Iskra.App.Services;
using Iskra.Core.Loc;
using Iskra.Core.Settings;

namespace Iskra.App.Views;

/// <summary>Раздел «Шрифты» — выбор системного шрифта независимо от темы, с живым предпросмотром и починкой.</summary>
internal sealed partial class SettingsWindow
{
    private static readonly string[] BundledFonts =
    {
        "Inter", "Open Sans", "Manrope", "Comfortaa", "Exo 2", "Lora", "JetBrains Mono",
        "Caveat", "Marck Script", "Amatic SC", "Neucha", "Pacifico", "Russo One", "Press Start 2P",
    };

    private string[]? _allFontsCache;
    private string[] AllFonts()
    {
        if (_allFontsCache != null) return _allFontsCache;
        var set = new List<string>(BundledFonts);
        try
        {
            var installed = Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase);
            foreach (var n in installed) if (!set.Contains(n, StringComparer.OrdinalIgnoreCase)) set.Add(n);
        }
        catch { }
        return _allFontsCache = set.ToArray();
    }

    private FrameworkElement BuildFontsPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1(L.T("nav.fonts")));
        root.Children.Add(Body(L.T("fonts.intro")));

        var f = _state.Settings.Fonts;
        TextBlock preview = new()
        {
            Text = L.T("fonts.previewText"),
            FontFamily = Theming.Font(f.TitleFont ?? f.SystemFont ?? "Segoe UI"),
            FontSize = 20, Foreground = T.Foreground, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 6),
        };
        void RefreshPreview() => preview.FontFamily = Theming.Font(f.TitleFont ?? f.SystemFont ?? "Segoe UI");

        root.Children.Add(Check(L.T("fonts.enable"), f.Enabled, v => { f.Enabled = v; _state.SaveSettings(); }));

        root.Children.Add(H2(L.T("fonts.areas")));
        root.Children.Add(LabeledRow(L.T("fonts.titlefont"), OptionalFontCombo(f.TitleFont, v => { f.TitleFont = v; _state.SaveSettings(); RefreshPreview(); })));
        root.Children.Add(LabeledRow(L.T("fonts.menufont"), OptionalFontCombo(f.MenuFont, v => { f.MenuFont = v; _state.SaveSettings(); })));
        root.Children.Add(LabeledRow(L.T("fonts.iconfont"), OptionalFontCombo(f.IconFont, v => { f.IconFont = v; _state.SaveSettings(); })));
        root.Children.Add(LabeledRow(L.T("fonts.messagefont"), OptionalFontCombo(f.MessageFont, v => { f.MessageFont = v; _state.SaveSettings(); })));
        root.Children.Add(LabeledRow(L.T("fonts.size"), Slider(0, 18, f.SizePt <= 0 ? 9 : f.SizePt, v => { f.SizePt = Math.Abs(v - 9) < 0.01 ? 0 : v; _state.SaveSettings(); })));

        // Предпросмотр.
        root.Children.Add(H2(L.T("fonts.preview")));
        root.Children.Add(preview);

        // Базовый UI-шрифт (подстановка Segoe UI) — требует прав администратора и повторного входа.
        root.Children.Add(H2("⚠ " + L.T("fonts.replace")));
        root.Children.Add(Body(L.T("fonts.replaceWarn")));
        root.Children.Add(LabeledRow(L.T("fonts.systemfont"), OptionalFontCombo(f.SystemFont, v => { f.SystemFont = v; _state.SaveSettings(); RefreshPreview(); })));
        root.Children.Add(Check(L.T("fonts.replaceEnable"), f.ReplaceSystemWide, v => { f.ReplaceSystemWide = v; _state.SaveSettings(); }));

        // Что где меняется (матрица).
        root.Children.Add(H2(L.T("fonts.matrix")));
        root.Children.Add(Body("• " + L.T("fonts.matrixLive")));
        root.Children.Add(Body("• " + L.T("fonts.matrixApp")));
        root.Children.Add(Body("• " + L.T("fonts.matrixLogin")));

        var status = new TextBlock { FontFamily = T.UiFont, FontSize = 12.5, Foreground = T.Accent, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(Btn(L.T("fonts.apply"), () =>
        {
            f.Enabled = true; _state.SaveSettings();
            status.Text = "…";
            System.Threading.Tasks.Task.Run(() =>
            {
                var problems = _state.ApplyFonts();
                Dispatcher.Invoke(() => status.Text = L.T("fonts.applied") + (problems.Count > 0 ? "\n" + L.T("fonts.fallback") + " " + string.Join("; ", problems) : ""));
            });
        }, true));
        buttons.Children.Add(Btn(L.T("fonts.repair"), () =>
        {
            status.Text = L.T("fonts.repairing");
            System.Threading.Tasks.Task.Run(() =>
            {
                bool ok = FontRepairEntry.RunElevated();
                Dispatcher.Invoke(() => status.Text = ok ? L.T("fonts.repaired") : L.T("fonts.repairFailed"));
            });
        }));
        root.Children.Add(buttons);
        root.Children.Add(status);
        return root;
    }

    /// <summary>Комбо выбора шрифта с пунктом «по умолчанию» (null).</summary>
    private ComboBox OptionalFontCombo(string? value, Action<string?> onChange)
    {
        var cb = new ComboBox { Width = 280, FontFamily = T.UiFont, MaxDropDownHeight = 360 };
        cb.Items.Add(new ComboBoxItem { Content = new TextBlock { Text = L.T("fonts.default"), FontFamily = T.UiFont, FontSize = 14 }, Tag = "" });
        foreach (var name in AllFonts())
            cb.Items.Add(new ComboBoxItem { Content = new TextBlock { Text = name, FontFamily = Theming.Font(name), FontSize = 14 }, Tag = name });
        cb.SelectedIndex = 0;
        if (!string.IsNullOrEmpty(value))
            for (int i = 1; i < cb.Items.Count; i++)
                if (cb.Items[i] is ComboBoxItem it && string.Equals((string)it.Tag, value, StringComparison.OrdinalIgnoreCase)) { cb.SelectedIndex = i; break; }
        cb.SelectionChanged += (_, _) =>
        {
            var tag = (string)((ComboBoxItem)cb.SelectedItem).Tag;
            onChange(string.IsNullOrEmpty(tag) ? null : tag);
        };
        return cb;
    }
}
