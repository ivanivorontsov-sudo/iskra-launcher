using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Iskra.App.Services;
using Iskra.Core.Themes;

namespace Iskra.App.Views;

/// <summary>Мелкие помощники построения визуала и анимаций, общие для окон Искры.</summary>
internal static class UiKit
{
    public static readonly Duration Fast = new(TimeSpan.FromMilliseconds(140));
    public static readonly Duration Med = new(TimeSpan.FromMilliseconds(240));

    public static TimeSpan Scaled(AppState s, double ms) =>
        TimeSpan.FromMilliseconds(ms / Math.Max(0.3, s.Settings.CurrentTheme().Effects.Speed) * (s.ReduceEffects() ? 0.4 : 1));

    public static IEasingFunction Spring => new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };
    public static IEasingFunction OutCubic => new CubicEase { EasingMode = EasingMode.EaseOut };
    public static IEasingFunction InCubic => new CubicEase { EasingMode = EasingMode.EaseIn };

    public static void PlayOpen(AppState s, FrameworkElement el, OpenAnimation anim, Action? done = null)
    {
        if (s.ReduceEffects()) { el.Opacity = 1; if (el.RenderTransform is ScaleTransform st) { st.ScaleX = 1; st.ScaleY = 1; } done?.Invoke(); return; }
        el.RenderTransformOrigin = new Point(0.5, 0.42);
        var scale = new ScaleTransform(1, 1);
        var trans = new TranslateTransform();
        var group = new TransformGroup();
        group.Children.Add(scale); group.Children.Add(trans);
        el.RenderTransform = group;
        var sb = new Storyboard();
        double d = s.Settings.CurrentTheme().Effects.Speed;
        TimeSpan T(double ms) => Scaled(s, ms);

        void Fade(double from) { var a = new DoubleAnimation(from, 1, new Duration(T(200))) { EasingFunction = OutCubic }; Storyboard.SetTarget(a, el); Storyboard.SetTargetProperty(a, new PropertyPath("Opacity")); sb.Children.Add(a); }
        void Scale(double from, IEasingFunction ease, double ms = 260)
        {
            foreach (var p in new[] { "ScaleX", "ScaleY" })
            {
                var a = new DoubleAnimation(from, 1, new Duration(T(ms))) { EasingFunction = ease };
                Storyboard.SetTarget(a, scale); Storyboard.SetTargetProperty(a, new PropertyPath(p)); sb.Children.Add(a);
            }
        }
        void Slide(double fromY, double ms = 260)
        {
            var a = new DoubleAnimation(fromY, 0, new Duration(T(ms))) { EasingFunction = OutCubic };
            Storyboard.SetTarget(a, trans); Storyboard.SetTargetProperty(a, new PropertyPath("Y")); sb.Children.Add(a);
        }

        switch (anim)
        {
            case OpenAnimation.Spring: Fade(0); Scale(0.88, Spring, 360); break;
            case OpenAnimation.ScaleFade: Fade(0); Scale(0.94, OutCubic); break;
            case OpenAnimation.SlideUp: Fade(0); Slide(60); break;
            case OpenAnimation.Drop: Fade(0); Slide(-50, 420); Scale(0.97, new BounceEase { Bounces = 2, Bounciness = 3, EasingMode = EasingMode.EaseOut }, 520); break;
            case OpenAnimation.Turnstile:
                el.RenderTransformOrigin = new Point(0, 0.5);
                var rot = new RotateTransform(-70); group.Children.Add(rot);
                var ra = new DoubleAnimation(-70, 0, new Duration(T(320))) { EasingFunction = OutCubic };
                Storyboard.SetTarget(ra, rot); Storyboard.SetTargetProperty(ra, new PropertyPath("Angle")); sb.Children.Add(ra); Fade(0); break;
            case OpenAnimation.Glitch: Fade(0); Scale(1.03, InCubic, 90); break;
            case OpenAnimation.Typewriter: Fade(0.2); break;
            case OpenAnimation.Stepped:
                var steps = new DoubleAnimationUsingKeyFrames();
                steps.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0)));
                steps.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.4, KeyTime.FromPercent(0.33)));
                steps.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.75, KeyTime.FromPercent(0.66)));
                steps.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(1)));
                steps.Duration = new Duration(T(240));
                Storyboard.SetTarget(steps, el); Storyboard.SetTargetProperty(steps, new PropertyPath("Opacity")); sb.Children.Add(steps); break;
            default: Fade(0); break;
        }
        if (done != null) sb.Completed += (_, _) => done();
        sb.Begin();
    }

    public static void PlayClose(AppState s, FrameworkElement el, Action onDone)
    {
        if (s.ReduceEffects()) { onDone(); return; }
        var sb = new Storyboard();
        var fade = new DoubleAnimation(el.Opacity, 0, new Duration(Scaled(s, 130))) { EasingFunction = InCubic };
        Storyboard.SetTarget(fade, el); Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity")); sb.Children.Add(fade);
        var scale = el.RenderTransform as TransformGroup;
        if (scale?.Children[0] is ScaleTransform st)
        {
            foreach (var p in new[] { "ScaleX", "ScaleY" })
            {
                var a = new DoubleAnimation(1, 0.92, new Duration(Scaled(s, 130))) { EasingFunction = InCubic };
                Storyboard.SetTarget(a, st); Storyboard.SetTargetProperty(a, new PropertyPath(p)); sb.Children.Add(a);
            }
        }
        sb.Completed += (_, _) => onDone();
        sb.Begin();
    }

    public static DropShadowEffect Glow(Color c, double blur = 18, double opacity = 0.9) =>
        new() { Color = c, BlurRadius = blur, ShadowDepth = 0, Opacity = opacity };

    public static TextBlock Glyph(string glyph, double size, Brush brush) => new()
    {
        Text = glyph,
        FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        FontSize = size, Foreground = brush,
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
    };

    public static void FadeIn(FrameworkElement el, AppState s)
    {
        if (s.ReduceEffects()) { el.Opacity = 1; return; }
        var a = new DoubleAnimation(0, 1, new Duration(Scaled(s, 180))) { EasingFunction = OutCubic };
        el.BeginAnimation(UIElement.OpacityProperty, a);
    }
}
