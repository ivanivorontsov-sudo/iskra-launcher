using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Iskra.App.Services;
using Iskra.Core.Themes;

namespace Iskra.App.Views;

/// <summary>Анимированный фон темы: сияние, звёзды, лепестки, сетка, строки развёртки, боке, пиксели.
/// Рисует на частоте кадров монитора; при «минимуме эффектов» — статично или пусто.</summary>
internal sealed class BackgroundFxControl : FrameworkElement
{
    private readonly AppState _state;
    private readonly List<Particle> _particles = new();
    private DateTime _last = DateTime.Now;
    private double _time;
    private bool _running;
    private BackgroundFx _fx;

    public BackgroundFxControl(AppState state)
    {
        _state = state;
        _fx = state.Settings.CurrentTheme().Effects.Background;
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
        IsHitTestVisible = false;
    }

    private void Start()
    {
        _fx = _state.Settings.CurrentTheme().Effects.Background;
        InitParticles();
        if (_fx == BackgroundFx.None) { InvalidateVisual(); return; }
        if (_state.ReduceEffects()) { InvalidateVisual(); return; } // статичный кадр
        if (!_running) { _running = true; CompositionTarget.Rendering += OnFrame; }
    }

    private void Stop()
    {
        if (_running) { _running = false; CompositionTarget.Rendering -= OnFrame; }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        double dt = Math.Min(0.05, (now - _last).TotalSeconds);
        _last = now; _time += dt;
        Step(dt);
        InvalidateVisual();
    }

    private sealed class Particle { public double X, Y, VX, VY, Size, Phase; public Color Color; }

    private void InitParticles()
    {
        _particles.Clear();
        var t = _state.Settings.CurrentTheme();
        var accent = Theming.Col(t.Palette.Accent);
        var accent2 = Theming.Col(t.Palette.Accent2);
        var rnd = new Random(_fx.GetHashCode());
        int count = _fx switch
        {
            BackgroundFx.Stars => 110, BackgroundFx.Petals => 26, BackgroundFx.Snow => 40,
            BackgroundFx.Bokeh => 18, BackgroundFx.Pixels => 40, _ => 0,
        };
        for (int i = 0; i < count; i++)
            _particles.Add(new Particle
            {
                X = rnd.NextDouble(), Y = rnd.NextDouble(),
                VX = (rnd.NextDouble() - 0.5) * 0.02, VY = rnd.NextDouble() * 0.05 + 0.01,
                Size = rnd.NextDouble() * 3 + 1, Phase = rnd.NextDouble() * 6.28,
                Color = rnd.NextDouble() < 0.5 ? accent : accent2,
            });
    }

    private void Step(double dt)
    {
        foreach (var p in _particles)
        {
            switch (_fx)
            {
                case BackgroundFx.Stars: p.Phase += dt * 2; break;
                case BackgroundFx.Petals:
                case BackgroundFx.Snow:
                    p.Y += p.VY * dt * 4; p.X += Math.Sin(_time + p.Phase) * 0.0006 + p.VX * dt;
                    if (p.Y > 1.1) { p.Y = -0.1; p.X = new Random().NextDouble(); }
                    break;
                case BackgroundFx.Bokeh: p.Phase += dt; break;
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var t = _state.Settings.CurrentTheme();
        var accent = Theming.Col(t.Palette.Accent);
        var accent2 = Theming.Col(t.Palette.Accent2);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        switch (_fx)
        {
            case BackgroundFx.Aurora: Aurora(dc, w, h, accent, accent2); break;
            case BackgroundFx.NeonGrid: NeonGrid(dc, w, h, accent); break;
            case BackgroundFx.Scanlines: Scanlines(dc, w, h, accent); break;
            case BackgroundFx.Paper: Paper(dc, w, h, accent); break;
            case BackgroundFx.Stars: Stars(dc, w, h); break;
            case BackgroundFx.Petals: case BackgroundFx.Snow: Falling(dc, w, h); break;
            case BackgroundFx.Bokeh: Bokeh(dc, w, h); break;
            case BackgroundFx.Pixels: Pixels(dc, w, h); break;
        }
        dc.Pop();
    }

    private void Aurora(DrawingContext dc, double w, double h, Color a, Color b)
    {
        for (int i = 0; i < 3; i++)
        {
            double cx = w * (0.3 + 0.2 * i + Math.Sin(_time * 0.3 + i) * 0.1);
            double cy = h * (0.2 + 0.2 * i);
            var col = i % 2 == 0 ? a : b;
            var rg = new RadialGradientBrush(Color.FromArgb(70, col.R, col.G, col.B), Color.FromArgb(0, col.R, col.G, col.B))
            { Center = new Point(cx / w, cy / h), GradientOrigin = new Point(cx / w, cy / h), RadiusX = 0.6, RadiusY = 0.5 };
            dc.DrawRectangle(rg, null, new Rect(0, 0, w, h));
        }
    }

    private void NeonGrid(DrawingContext dc, double w, double h, Color a)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, a.R, a.G, a.B)), 1);
        double off = (_time * 30) % 32;
        for (double y = off; y < h; y += 32) dc.DrawLine(pen, new Point(0, y), new Point(w, y));
        for (double x = 0; x < w; x += 32) dc.DrawLine(pen, new Point(x, 0), new Point(x, h));
    }

    private void Scanlines(DrawingContext dc, double w, double h, Color a)
    {
        var br = new SolidColorBrush(Color.FromArgb(16, a.R, a.G, a.B));
        for (double y = 0; y < h; y += 3) dc.DrawRectangle(br, null, new Rect(0, y, w, 1));
    }

    private void Paper(DrawingContext dc, double w, double h, Color a)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(32, a.R, a.G, a.B)), 1);
        for (double y = 28; y < h; y += 26) dc.DrawLine(pen, new Point(14, y), new Point(w - 8, y));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(70, 0xD9, 0x53, 0x4F)), 1.4), new Point(44, 0), new Point(44, h));
    }

    private void Stars(DrawingContext dc, double w, double h)
    {
        foreach (var p in _particles)
        {
            double tw = 0.5 + 0.5 * Math.Sin(p.Phase);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(tw * 180 + 40), 255, 255, 255)), null, new Point(p.X * w, p.Y * h), p.Size * 0.6, p.Size * 0.6);
        }
    }

    private void Falling(DrawingContext dc, double w, double h)
    {
        foreach (var p in _particles)
        {
            var c = _fx == BackgroundFx.Snow ? Colors.White : p.Color;
            var br = new SolidColorBrush(Color.FromArgb(150, c.R, c.G, c.B));
            var ctr = new Point(p.X * w, p.Y * h);
            if (_fx == BackgroundFx.Petals)
            {
                dc.PushTransform(new RotateTransform(_time * 40 + p.Phase * 50, ctr.X, ctr.Y));
                dc.DrawEllipse(br, null, ctr, p.Size * 1.6, p.Size);
                dc.Pop();
            }
            else dc.DrawEllipse(br, null, ctr, p.Size, p.Size);
        }
    }

    private void Bokeh(DrawingContext dc, double w, double h)
    {
        foreach (var p in _particles)
        {
            double r = p.Size * 8 * (0.7 + 0.3 * Math.Sin(p.Phase));
            var c = p.Color;
            var rg = new RadialGradientBrush(Color.FromArgb(50, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B));
            dc.DrawEllipse(rg, null, new Point(p.X * w, p.Y * h), r, r);
        }
    }

    private void Pixels(DrawingContext dc, double w, double h)
    {
        double s = 10;
        foreach (var p in _particles)
        {
            var c = p.Color;
            double bx = Math.Floor(p.X * w / s) * s, by = Math.Floor(((p.Y + _time * 0.03) % 1) * h / s) * s;
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(90, c.R, c.G, c.B)), null, new Rect(bx, by, s, s));
        }
    }
}
