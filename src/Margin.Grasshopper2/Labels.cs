using Eto.Drawing;
using Grasshopper2.Components;
using Grasshopper2.Parameters;
using Grasshopper2.UI.Canvas;
using Margin.Core;
using Margin.Native;

namespace Margin.GH2;

public sealed class Labels : ILabelRenderer
{
#if MARGIN_MAC
    // Eto's Mac graphics use one logical pixel per point. Windows uses 96/72.
    const float FontScale = 1f;
#else
    const float FontScale = .75f;
#endif
    readonly Canvas canvas;
    public Theme Theme { get; set; } = new();
    public bool Enabled { get; set; }
    public string? LastError { get; private set; }
    public int LastDrawn { get; private set; }
    public double LastPaintMilliseconds { get; private set; }
    public Labels(Canvas canvas) { this.canvas = canvas; canvas.AfterPaintObjects += Paint; }
    void Paint(object? sender, CanvasPaintEventArgs e)
    {
        LastDrawn = 0;
        LastPaintMilliseconds = 0;
        var document = canvas.Document;
        if (!Enabled || document is null || e.Graphics.Zoom < Theme.MinZoom) return;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var state = e.Graphics.ProjectionState();
            var g = e.Graphics.Content;
            var theme = Theme.Normalize();
            using var font = new Font(theme.FontFamily, (float)theme.FontSize * FontScale, theme.Bold ? FontStyle.Bold : FontStyle.None);
            using var small = new Font(theme.FontFamily, Math.Max(8, (float)theme.FontSize - 2) * FontScale);
            // Keep edge labels and nearby collision blockers, but avoid measuring the
            // entire definition on every pan or redraw. Names are capped at 64 chars.
            var longest = g.MeasureString(font, new string('W', 64));
            var frame = canvas.VisibleFrame;
            var marginX = longest.Width / 2 + (float)theme.PaddingX + 10;
            var marginY = longest.Height * 2 + (float)(theme.PaddingY * 2 + theme.Gap) + 10;
            var nearby = new Box(frame.X - marginX, frame.Y - marginY,
                frame.Width + marginX * 2, frame.Height + marginY * 2);
            var items = document.Objects.Forwards
                .Where(o => o is Component || o is IParameter)
                .Where(o => o.Attributes is not null && From(o.Attributes.Bounds) is var b && b.Valid && b.Intersects(nearby))
                .ToArray();
            var occupied = items.Select(o => From(o.Attributes.Bounds)).ToList();
            var headings = document.Objects.Groups.Select(group => group.Attributes is StyledGroup styled
                    ? styled.TitleRegion : RectangleF.Empty)
                .Select(From).Where(box => box.Valid && box.Intersects(nearby)).ToArray();
            using var fill = new SolidBrush(Colour(theme.Fill, theme.Opacity / 100));
            using var ink = new SolidBrush(Colour(theme.Text));
            using var caption = new SolidBrush(Colour(theme.Text, .75));
            using var accent = new SolidBrush(Colour(theme.Accent));
            using var border = new Pen(Colour(theme.Border), (float)Math.Max(.01, theme.BorderWidth));
            foreach (var item in items.OrderByDescending(o => o.Selected))
            {
                if (theme.SelectedOnly && !item.Selected) continue;
                var name = theme.UseNickname ? item.DisplayName : item.Nomen.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.Length > 64) name = name[..61] + "…";
                var category = item.Nomen.Chapter ?? "";
                if (category.Length > 32) category = category[..29] + "…";
                var size = g.MeasureString(font, name);
                var sub = theme.ShowCategory ? g.MeasureString(small, category) : SizeF.Empty;
                float extra = theme.ShowAccent ? 5 : 0;
                var width = Math.Max(size.Width, sub.Width) + theme.PaddingX * 2 + extra;
                var height = size.Height + (theme.ShowCategory ? sub.Height + 3 : 0) + theme.PaddingY * 2;
                var place = LabelLayout.Place(From(item.Attributes.Bounds), width, height, theme, occupied, headings);
                if (place is not Box box) continue;
                var rect = new RectangleF((float)box.X, (float)box.Y, (float)box.Width, (float)box.Height);
                using var path = new GraphicsPath();
                var radius = (float)Math.Min(theme.Radius, Math.Min(box.Width, box.Height) / 2);
                if (radius > 0)
                {
                    var d = radius * 2;
                    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                }
                else path.AddRectangle(rect);
                if (theme.Opacity > 0) g.FillPath(fill, path);
                if (theme.BorderWidth > 0) g.DrawPath(border, path);
                float x = rect.X + (float)theme.PaddingX, y = rect.Y + (float)theme.PaddingY;
                if (theme.ShowAccent) g.FillRectangle(accent, x - 3, y, 2, rect.Height - (float)theme.PaddingY * 2);
                g.DrawText(font, ink, x + extra, y, name);
                if (theme.ShowCategory) g.DrawText(small, caption, x + extra, y + size.Height + 3, category);
                occupied.Add(box); LastDrawn++;
            }
        }
        catch (Exception ex)
        {
            Enabled = false; LastError = ex.Message;
            canvas.AfterPaintObjects -= Paint;
            Rhino.RhinoApp.WriteLine("Margin GH2 labels paused: " + ex.Message);
        }
        finally { LastPaintMilliseconds = watch.Elapsed.TotalMilliseconds; }
    }
    static Box From(RectangleF b) => new(b.X, b.Y, b.Width, b.Height);
    static Color Colour(string hex, double alpha = 1) => new Color(
        Convert.ToInt32(hex[1..3],16)/255f, Convert.ToInt32(hex[3..5],16)/255f,
        Convert.ToInt32(hex[5..7],16)/255f, (float)alpha);
    public void Dispose() { Enabled = false; canvas.AfterPaintObjects -= Paint; }
}
