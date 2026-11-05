namespace Margin.Core;

public readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public bool Intersects(Box b) => X < b.Right && Right > b.X && Y < b.Bottom && Bottom > b.Y;
    public Box Inflate(double n) => new(X - n, Y - n, Width + 2 * n, Height + 2 * n);
    public bool Valid => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
}

public static class LabelLayout
{
    // Keep labels associated with their component. If all nearby placements collide,
    // omit the label instead of drifting it across someone else's definition.
    public static Box? Place(Box component, double width, double height, Theme theme, IReadOnlyList<Box> occupied)
    {
        if (!component.Valid || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return null;
        double x = component.CenterX - width / 2;
        var preferred = new Box(x, theme.Below ? component.Bottom + theme.Gap : component.Y - theme.Gap - height, width, height);
        if (!theme.AvoidOverlap) return preferred;
        var alternatives = new[] { preferred, new Box(x, theme.Below ? component.Y - theme.Gap - height : component.Bottom + theme.Gap, width, height) };
        foreach (var b in alternatives) if (!occupied.Any(o => o.Intersects(b.Inflate(3)))) return b;
        return null;
    }
}

public readonly record struct SnapResult(double Dx, double Dy, double? GuideX, double? GuideY);
public static class SnapEngine
{
    public static SnapResult Find(Box moving, IEnumerable<Box> targets, double zoom, double radiusPixels = 7)
    {
        if (!moving.Valid || !double.IsFinite(zoom) || zoom <= 0) return default;
        double tolerance = Math.Clamp(radiusPixels, 1, 24) / zoom;
        double dx = 0, dy = 0, bestX = tolerance + 1e-8, bestY = tolerance + 1e-8; double? gx = null, gy = null;
        foreach (var t in targets.Where(t => t.Valid).OrderBy(t => t.X).ThenBy(t => t.Y))
        {
            // Match like anchors: left-left, centre-centre, right-right.
            foreach (var pair in new[] { (moving.X, t.X), (moving.CenterX, t.CenterX), (moving.Right, t.Right) })
            {
                var delta = pair.Item2 - pair.Item1;
                if (Math.Abs(delta) < bestX) { bestX = Math.Abs(delta); dx = delta; gx = pair.Item2; }
            }
            foreach (var pair in new[] { (moving.Y, t.Y), (moving.CenterY, t.CenterY), (moving.Bottom, t.Bottom) })
            {
                var delta = pair.Item2 - pair.Item1;
                if (Math.Abs(delta) < bestY) { bestY = Math.Abs(delta); dy = delta; gy = pair.Item2; }
            }
        }
        return new(dx, dy, gx, gy);
    }
}
