using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Iskra.App.Services;
using Iskra.Core.Loc;
using Iskra.Core.Themes;

namespace Iskra.App;

/// <summary>Значок в системном трее с меню: открыть, стартовый экран, темы, настройки, восстановление, выход.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly AppState _state;
    private readonly NotifyIcon _icon;

    public TrayIcon(AppState state)
    {
        _state = state;
        _icon = new NotifyIcon
        {
            Icon = BuildIcon(Theming.Col(state.Settings.CurrentTheme().Palette.Accent)),
            Text = "Искра",
            Visible = false,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) _state.ToggleLauncher(); };
        _icon.ContextMenuStrip = BuildMenu();
        _state.ThemeChanged += () => { try { _icon.Icon = BuildIcon(Theming.Col(_state.Settings.CurrentTheme().Palette.Accent)); _icon.ContextMenuStrip = BuildMenu(); } catch { } };
    }

    public void Show()
    {
        _icon.Visible = true;
        var hk = _state.Settings.Hotkey;
        try { _icon.ShowBalloonTip(4000, "Искра", L.F("tray.running", hk), ToolTipIcon.None); } catch { }
    }

    private ContextMenuStrip BuildMenu()
    {
        var m = new ContextMenuStrip();
        m.Items.Add(L.T("tray.open"), null, (_, _) => _state.ToggleLauncher());
        m.Items.Add(L.T("tray.start"), null, (_, _) => _state.ToggleStartScreen());
        m.Items.Add(new ToolStripSeparator());

        var themes = new ToolStripMenuItem(L.T("tray.themes"));
        foreach (var t in _state.Settings.AllThemes())
        {
            var item = new ToolStripMenuItem(t.DisplayName(L.English)) { Checked = t.Id == _state.Settings.ThemeId };
            var captured = t;
            item.Click += (_, _) => _ = _state.ApplyThemeAsync(captured, true);
            themes.DropDownItems.Add(item);
        }
        m.Items.Add(themes);
        m.Items.Add(L.T("tray.settings"), null, (_, _) => _state.OpenSettings());
        m.Items.Add(new ToolStripSeparator());

        if (_state.System.HasSnapshot)
            m.Items.Add(L.T("tray.restore"), null, (_, _) => _state.RestoreWindows());

        var pause = new ToolStripMenuItem(L.T("tray.pause")) { Checked = !_state.Settings.ReplaceStartMenu };
        pause.Click += (_, _) => { _state.Settings.ReplaceStartMenu = !_state.Settings.ReplaceStartMenu; _state.SaveSettings(); };
        m.Items.Add(pause);

        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(L.T("tray.exit"), null, (_, _) => Program.Quit());
        return m;
    }

    /// <summary>Рисуем «искру» (четырёхлучевая звезда) цветом акцента — без логотипов.</summary>
    public static Icon BuildIcon(System.Windows.Media.Color accent)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var c = Color.FromArgb(accent.A, accent.R, accent.G, accent.B);
            using var br = new SolidBrush(c);
            PointF ctr = new(16, 16);
            var pts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                double ang = Math.PI / 4 * i - Math.PI / 2;
                double r = i % 2 == 0 ? 15 : 5;
                pts[i] = new PointF((float)(ctr.X + Math.Cos(ang) * r), (float)(ctr.Y + Math.Sin(ang) * r));
            }
            g.FillPolygon(br, pts);
            using var core = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            g.FillEllipse(core, 13, 13, 6, 6);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
