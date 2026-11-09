using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Margin.Core;

namespace Margin.Native;

public sealed class Labels : ILabelRenderer
{
    readonly GH_Canvas canvas;
    public Theme Theme { get; set; } = new();
    public bool Enabled { get; set; }
    public string? LastError { get; private set; }
    public int LastDrawn { get; private set; }
    public Labels(GH_Canvas canvas)
    {
        this.canvas = canvas;
        canvas.CanvasPostPaintObjects += Paint;
    }
    void Paint(GH_Canvas sender)
    {
        if (!Enabled || sender.Document is null || sender.Viewport.Zoom < Theme.MinZoom) return;
        try
        {
            var items = sender.Document.Objects
                .Where(o => o is IGH_Component || o is IGH_Param)
                .Where(o => o.Attributes is not null)
                .Select(o => new LabelItem(o.Name, o.NickName, o.Category, From(o.Attributes.Bounds), o.Attributes.Selected))
                .ToArray();
            // Component labels must leave the group heading readable, including None.
            var headings = sender.Document.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>()
                .Where(g => Groups.Style(g).Designed && g.Attributes is StyledGroup)
                .Select(g => From(((StyledGroup)g.Attributes).TitleRegion)).ToArray();
            LastDrawn = Draw(sender.Graphics, items, Theme, headings);
        }
        catch (Exception ex)
        {
            Enabled = false;
            LastError = ex.Message;
            // Unhook immediately: a bad overlay must never poison later paint passes.
            canvas.CanvasPostPaintObjects -= Paint;
            Rhino.RhinoApp.WriteLine("Margin labels paused: " + ex.Message);
        }
    }
    public static Box From(RectangleF b) => new(b.X, b.Y, b.Width, b.Height);
    public static Color ColorOf(string hex, int alpha = 255) => Color.FromArgb(alpha, Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));
    public static int Draw(Graphics graphics, IReadOnlyList<LabelItem> items, Theme source, IReadOnlyList<Box>? exclusions = null)
    {
        var theme = source.Normalize();
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var font = new Font(theme.FontFamily, (float)theme.FontSize, theme.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            using var caption = new Font(theme.FontFamily, Math.Max(8, (float)theme.FontSize - 2), FontStyle.Regular, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(ColorOf(theme.Text));
            using var categoryBrush = new SolidBrush(ColorOf(theme.Text, 190));
            using var fill = new SolidBrush(ColorOf(theme.Fill, (int)Math.Round(theme.Opacity * 2.55)));
            using var border = new Pen(ColorOf(theme.Border), (float)Math.Max(.01, theme.BorderWidth));
            using var accent = new SolidBrush(ColorOf(theme.Accent));
            using var format = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap };
            var occupied = items.Where(i => i.Bounds.Valid).Select(i => i.Bounds).ToList();
            if (exclusions is not null) occupied.AddRange(exclusions.Where(b => b.Valid));
            int drawn = 0;
            foreach (var item in items.OrderByDescending(i => i.Selected))
            {
                if (theme.SelectedOnly && !item.Selected || !item.Bounds.Valid) continue;
                string name = theme.UseNickname ? item.Nickname : item.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.Length > 64) name = name[..61] + "…";
                string category = item.Category.Length > 32 ? item.Category[..29] + "…" : item.Category;
                var size = graphics.MeasureString(name, font, 1000, format);
                var cat = theme.ShowCategory ? graphics.MeasureString(category, caption, 1000, format) : SizeF.Empty;
                float extra = theme.ShowAccent ? 5 : 0;
                float width = Math.Max(size.Width, cat.Width) + (float)theme.PaddingX * 2 + extra;
                float height = size.Height + (theme.ShowCategory ? cat.Height + 3 : 0) + (float)theme.PaddingY * 2;
                var placed = LabelLayout.Place(item.Bounds, width, height, theme, occupied);
                if (placed is not Box box) continue;
                using var path = Round(box, (float)theme.Radius);
                if (theme.Opacity > 0) graphics.FillPath(fill, path);
                if (theme.BorderWidth > 0) graphics.DrawPath(border, path);
                float x = (float)box.X + (float)theme.PaddingX;
                float y = (float)box.Y + (float)theme.PaddingY;
                if (theme.ShowAccent) graphics.FillRectangle(accent, x - 3, y, 2, height - (float)theme.PaddingY * 2);
                x += extra;
                graphics.DrawString(name, font, textBrush, x, y, format);
                if (theme.ShowCategory) graphics.DrawString(category, caption, categoryBrush, x, y + size.Height + 3, format);
                occupied.Add(box);
                drawn++;
            }
            return drawn;
        }
        finally { graphics.Restore(state); }
    }
    static GraphicsPath Round(Box b, float radius)
    {
        var p = new GraphicsPath();
        var r = new RectangleF((float)b.X, (float)b.Y, (float)b.Width, (float)b.Height);
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d < .1f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    public void Dispose() { Enabled = false; canvas.CanvasPostPaintObjects -= Paint; }
}
public sealed record LabelItem(string Name, string Nickname, string Category, Box Bounds, bool Selected = false);
