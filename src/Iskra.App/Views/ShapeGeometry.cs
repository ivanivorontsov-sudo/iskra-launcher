using System;
using System.Windows;
using System.Windows.Media;
using Iskra.Core.Themes;

namespace Iskra.App.Views;

/// <summary>Строит геометрию маски значка (капля, круг, сквиркл, шестиугольник, ромб, звезда, лист, клякса, пиксель…).</summary>
internal static class ShapeGeometry
{
    public static Geometry For(IconShape shape, double s)
    {
        Geometry g = shape switch
        {
            IconShape.Circle => new EllipseGeometry(new Rect(0, 0, s, s)),
            IconShape.Pixel => new RectangleGeometry(new Rect(0, 0, s, s)),
            IconShape.RoundedSquare => new RectangleGeometry(new Rect(0, 0, s, s), s * 0.22, s * 0.22),
            IconShape.Squircle => Squircle(s),
            IconShape.Droplet => Droplet(s),
            IconShape.Hexagon => Polygon(s, 6, -Math.PI / 2),
            IconShape.Octagon => Polygon(s, 8, Math.PI / 8),
            IconShape.Rhombus => Polygon(s, 4, -Math.PI / 2),
            IconShape.RoundedTriangle => RoundedTriangle(s),
            IconShape.Leaf => Leaf(s),
            IconShape.Star => Star(s, 5),
            IconShape.Blob => Blob(s),
            _ => new RectangleGeometry(new Rect(0, 0, s, s), s * 0.22, s * 0.22),
        };
        g.Freeze();
        return g;
    }

    private static Geometry Squircle(double s)
    {
        // Суперэллипс ~ закруглённый квадрат с большим радиусом.
        double r = s * 0.42;
        return new RectangleGeometry(new Rect(0, 0, s, s), r, r);
    }

    private static Geometry Polygon(double s, int n, double start)
    {
        double cx = s / 2, cy = s / 2, rad = s / 2;
        var fig = new PathFigure { IsClosed = true, IsFilled = true };
        for (int i = 0; i < n; i++)
        {
            double a = start + i * 2 * Math.PI / n;
            var p = new Point(cx + rad * Math.Cos(a), cy + rad * Math.Sin(a));
            if (i == 0) fig.StartPoint = p; else fig.Segments.Add(new LineSegment(p, true));
        }
        var pg = new PathGeometry(); pg.Figures.Add(fig); return pg;
    }

    private static Geometry Star(double s, int points)
    {
        double cx = s / 2, cy = s / 2, outer = s / 2, inner = s * 0.21;
        var fig = new PathFigure { IsClosed = true, IsFilled = true };
        for (int i = 0; i < points * 2; i++)
        {
            double r = i % 2 == 0 ? outer : inner;
            double a = -Math.PI / 2 + i * Math.PI / points;
            var p = new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
            if (i == 0) fig.StartPoint = p; else fig.Segments.Add(new LineSegment(p, true));
        }
        var pg = new PathGeometry(); pg.Figures.Add(fig); return pg;
    }

    private static Geometry Droplet(double s)
    {
        // Капля: острый верх, круглый низ.
        double cx = s / 2, r = s * 0.5;
        var fig = new PathFigure { StartPoint = new Point(cx, 0), IsClosed = true, IsFilled = true };
        fig.Segments.Add(new BezierSegment(new Point(s, s * 0.18), new Point(s, s * 0.62), new Point(cx, s), true));
        fig.Segments.Add(new BezierSegment(new Point(0, s * 0.62), new Point(0, s * 0.18), new Point(cx, 0), true));
        var pg = new PathGeometry(); pg.Figures.Add(fig); return pg;
    }

    private static Geometry Leaf(double s)
    {
        // Лист: два острых противоположных угла, два скруглённых.
        double k = s * 0.5;
        var fig = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true, IsFilled = true };
        fig.Segments.Add(new BezierSegment(new Point(k, 0), new Point(s, 0), new Point(s, s), true));
        fig.Segments.Add(new BezierSegment(new Point(s - k, s), new Point(0, s), new Point(0, 0), true));
        var pg = new PathGeometry(); pg.Figures.Add(fig); return pg;
    }

    private static Geometry RoundedTriangle(double s)
    {
        double cx = s / 2;
        var fig = new PathFigure { StartPoint = new Point(cx, s * 0.06), IsClosed = true, IsFilled = true };
        // три вершины со скруглением через короткие дуги
        fig.Segments.Add(new LineSegment(new Point(s * 0.92, s * 0.86), true));
        fig.Segments.Add(new ArcSegment(new Point(s * 0.72, s * 0.94), new Size(s * 0.1, s * 0.1), 0, false, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(new Point(s * 0.28, s * 0.94), true));
        fig.Segments.Add(new ArcSegment(new Point(s * 0.08, s * 0.86), new Size(s * 0.1, s * 0.1), 0, false, SweepDirection.Clockwise, true));
        var pg = new PathGeometry(); pg.Figures.Add(fig); return pg;
    }

    private static Geometry Blob(double s)
    {
        // Органичная «клякса» из четырёх кубических кривых (фиксированная форма).
        double c = s / 2;
        var fig = new PathFigure { StartPoint = new Point(c, s * 0.02), IsClosed = true, IsFilled = true };
        fig.Segments.Add(new BezierSegment(new Point(s * 0.95, s * 0.08), new Point(s * 1.02, s * 0.52), new Point(s * 0.86, s * 0.78), true));
        fig.Segments.Add(new BezierSegment(new Point(s * 0.72, s * 1.0), new Point(s * 0.3, s * 1.02), new Point(s * 0.16, s * 0.8), true));
        fig.Segments.Add(new BezierSegment(new Point(s * 0.0, s * 0.56), new Point(s * 0.02, s * 0.2), new Point(s * 0.28, s * 0.08), true));
        fig.Segments.Add(new BezierSegment(new Point(s * 0.38, s * 0.0), new Point(s * 0.5, s * 0.0), new Point(c, s * 0.02), true));
        var pg = new PathGeometry(); pg.Figures.Add(fig); return pg;
    }
}
