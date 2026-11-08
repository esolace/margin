using Margin.Core;

#if MARGIN_GH2
using Eto.Drawing;
using Grasshopper2.SpecialObjects;
using Grasshopper2.Eto.Drawing;
using Grasshopper2.UI.Skinning;
#else
using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.Kernel.Special;
using Grasshopper.GUI.Canvas;
#endif

namespace Margin.Native;

#if MARGIN_GH2
public sealed class StyledGroup : GroupAttributes
{
    readonly GroupObject group;
    bool failed;
    public StyledGroup(GroupObject group) : base(group) => this.group = group;
    public override void Layout(Shape shape)
    {
        base.Layout(shape);
        if (!Groups.Style(group).Designed) return;
        var b = Bounds; b.Inflate(12, 12); var header = 48 + (float)Groups.Style(group).Appearance.ExtraHeaderSpace; b.Y -= header; b.Height += header;
        if (b.Width < 180) b.Width = 180;
        Bounds = b;
    }
    public override void Draw(Context context, Skin skin)
    {
        var style = Groups.Style(group);
        if (failed || !style.Designed) { base.Draw(context, skin); return; }
        try
        {
            var b = Bounds;
            var appearance = style.Appearance;
            var g = context.Graphics;
            var fill = Color.Parse(style.Fill); fill.A = (float)(style.Opacity / 100);
            var resolved = GroupAppearancePolicy.Resolve(appearance, style.Fill, style.Opacity, context.BackColour.ToHex(false));
            var accentColour = Color.Parse(resolved.Accent);
            var borderColour = Color.Parse(resolved.Border);
            using var path = GroupFrame.Path(b.X,b.Y,b.Width,b.Height,(float)appearance.Radius);
            using var brush = new SolidBrush(fill);
            g.FillPath(brush,path);
            if (resolved.Band)
            {
                using var band = GroupFrame.Path(b.X,b.Y,b.Width,(float)appearance.TitleSize*1.3f+24,(float)appearance.Radius,true);
                using var bandFill = new SolidBrush(accentColour); g.FillPath(bandFill,band);
            }
            if (appearance.BorderWidth > 0 || group.Selected)
            {
                using var pen = new Pen(borderColour, group.Selected ? Math.Max(2.5f,(float)appearance.BorderWidth) : (float)appearance.BorderWidth);
                g.DrawPath(pen,path);
            }
            if (appearance.ShowAccent)
            { using var accent = new SolidBrush(accentColour); g.FillRectangle(accent,b.X+1,b.Y+10,4,b.Height-20); }
#if MARGIN_MAC
            const float scale = 1;
#else
            const float scale = .75f;
#endif
            using var font = new Font(SystemFonts.Default().Family, (float)appearance.TitleSize * scale, appearance.Bold ? FontStyle.Bold : FontStyle.None);
            string name = string.IsNullOrWhiteSpace(group.UserName) ? "Untitled group" : group.UserName;
            name = GroupAppearancePolicy.FormatTitle(name, appearance.TitleCase);
            while (name.Length > 2 && g.MeasureString(font,name).Width > b.Width - 34) name = name[..^2] + "…";
            var ink = Color.Parse(resolved.Text);
            using var text = new SolidBrush(ink); g.DrawText(font,text,b.X+18,b.Y+12,name);
            if (resolved.Divider)
            { using var rule = new Pen(accentColour,1.5f); var y=b.Y+(float)appearance.TitleSize*1.3f+17; g.DrawLine(rule,b.X+18,y,b.X+Math.Min(b.Width-18,170),y); }
        }
        catch (Exception ex) { failed = true; Rhino.RhinoApp.WriteLine("Margin group styling paused: " + ex.Message); base.Draw(context,skin); }
    }
}
#else
public sealed class StyledGroup : GH_GroupAttributes, Grasshopper.Kernel.IGH_Attributes
{
    readonly GH_Group group;
    bool failed;
    RectangleF[] frameContents = [];
    public StyledGroup(GH_Group group) : base(group) => this.group = group;
    RectangleF Frame()
    {
        // The native renderer normally invalidates its cached outline when members
        // move. Our renderer replaces that pass, so refresh the same cache here.
        // Doing this before bounds/picking as well as painting keeps dragging and
        // nested groups in sync without expiring layouts on every paint.
        var contents = group.ContentBoxes();
        if (contents is null || !contents.SequenceEqual(frameContents))
        {
            base.ExpireLayout();
            frameContents = contents?.ToArray() ?? [];
        }
        var b = base.Bounds;
        if (b.IsEmpty) return b;
        b.Inflate(12,12); var header = 85 + (float)Groups.Style(group).Appearance.ExtraHeaderSpace; b.Y -= header; b.Height += header;
        if(b.Width < 180) b.Width = 180;
        return b;
    }
    static RectangleF TitleBounds(RectangleF b, GroupAppearance appearance) => new(b.X+18,b.Y+10,b.Width-34,(float)appearance.TitleSize*1.5f+7);
    public new RectangleF Bounds { get => Groups.Style(group).Designed ? Frame() : base.Bounds; set => base.Bounds = value; }
    public new bool IsPickRegion(PointF point) => Groups.Style(group).Designed ? Frame().Contains(point) : base.IsPickRegion(point);
    public new bool IsPickRegion(RectangleF rectangle, Grasshopper.Kernel.GH_PickBox pick) => Groups.Style(group).Designed ? (pick == Grasshopper.Kernel.GH_PickBox.Window ? rectangle.Contains(Frame()) : rectangle.IntersectsWith(Frame())) : base.IsPickRegion(rectangle, pick);
    public new bool IsMenuRegion(PointF point) => Groups.Style(group).Designed ? Frame().Contains(point) : base.IsMenuRegion(point);
    public new GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, Grasshopper.GUI.GH_CanvasMouseEvent e)
    {
        if (Groups.Style(group).Designed && TitleBounds(Frame(), Groups.Style(group).Appearance).Contains(e.CanvasLocation))
        {
            Eto.Forms.Application.Instance.AsyncInvoke(() => GroupEditor.Show(group));
            return GH_ObjectResponse.Handled;
        }
        return base.RespondToMouseDoubleClick(sender, e);
    }
    public new void RenderToCanvas(GH_Canvas canvas, GH_CanvasChannel channel)
    {
        var style = Groups.Style(group);
        if (failed || !style.Designed) { base.RenderToCanvas(canvas,channel); return; }
        if(channel != GH_CanvasChannel.Groups) return;
        var g = canvas.Graphics; var state = g.Save();
        try
        {
            var b = Frame(); if(b.Width <= 0 || b.Height <= 0) return;
            var appearance = style.Appearance;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var colour = group.Colour.IsEmpty ? GH_Skin.group_back : group.Colour;
            var background = GH_Skin.canvas_back;
            var canvasColour = $"#{background.R:X2}{background.G:X2}{background.B:X2}";
            var resolved = GroupAppearancePolicy.Resolve(appearance, style.Fill, colour.A / 255d * 100, canvasColour);
            var accentColour = ColorTranslator.FromHtml(resolved.Accent);
            var borderColour = ColorTranslator.FromHtml(resolved.Border);
            using var path = GroupFrame.Path(b.X,b.Y,b.Width,b.Height,(float)appearance.Radius);
            using var fill = new SolidBrush(colour); g.FillPath(fill,path);
            if (resolved.Band)
            {
                using var band = GroupFrame.Path(b.X,b.Y,b.Width,(float)appearance.TitleSize*1.3f+24,(float)appearance.Radius,true);
                using var bandFill = new SolidBrush(accentColour); g.FillPath(bandFill,band);
            }
            if (appearance.BorderWidth > 0 || Selected)
            {
                using var pen = new Pen(borderColour, Selected ? Math.Max(2.5f,(float)appearance.BorderWidth) : (float)appearance.BorderWidth);
                g.DrawPath(pen,path);
            }
            if (appearance.ShowAccent)
            { using var accent = new SolidBrush(accentColour);g.FillRectangle(accent,b.X+1,b.Y+10,4,b.Height-20); }
            using var font = new Font(SystemFonts.DefaultFont.FontFamily,(float)appearance.TitleSize,appearance.Bold ? FontStyle.Bold : FontStyle.Regular,GraphicsUnit.Pixel);
            string name = string.IsNullOrWhiteSpace(group.NickName)?"Untitled group":group.NickName;
            name = GroupAppearancePolicy.FormatTitle(name, appearance.TitleCase);
            var ink = ColorTranslator.FromHtml(resolved.Text);
            using var text = new SolidBrush(ink);
            using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var titleBounds = TitleBounds(b, appearance);
            g.DrawString(name,font,text,titleBounds,format);
            if (resolved.Divider)
            { using var rule = new Pen(accentColour,1.5f);var y=b.Y+(float)appearance.TitleSize*1.3f+17;g.DrawLine(rule,b.X+18,y,b.X+Math.Min(b.Width-18,170),y); }
        }
        catch(Exception ex) { failed=true;Rhino.RhinoApp.WriteLine("Margin group styling paused: "+ex.Message); }
        finally { g.Restore(state); }
    }
}
#endif

internal static class GroupFrame
{
    public static GraphicsPath Path(float x, float y, float width, float height, float radius, bool topOnly = false)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(width, topOnly ? height * 2 : height));
        if (d < .1f) { path.AddRectangle(new RectangleF(x,y,width,height)); return path; }
        path.AddArc(x,y,d,d,180,90); path.AddArc(x+width-d,y,d,d,270,90);
        if (topOnly)
        { path.AddLine(x+width,y+d/2,x+width,y+height);path.AddLine(x+width,y+height,x,y+height); }
        else
        { path.AddArc(x+width-d,y+height-d,d,d,0,90);path.AddArc(x,y+height-d,d,d,90,90); }
        path.CloseFigure(); return path;
    }
}
