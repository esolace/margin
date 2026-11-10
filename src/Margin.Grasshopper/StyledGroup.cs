using Margin.Core;

#if MARGIN_GH2
using Eto.Drawing;
using Grasshopper2.SpecialObjects;
using Grasshopper2.Eto.Drawing;
using Grasshopper2.UI.Skinning;
using Grasshopper2.Doc.Attributes;
using Grasshopper2.Extensions;
using Grasshopper2.UI.Animation;
using System.Runtime.CompilerServices;
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
    internal RectangleF TitleRegion { get; private set; }
    long frameTick;
    Color paintedFill, paintedBand;
    RectangleF bandBounds;

    // GH2's selection and cursor code read this private-set property directly.
    // Share its native hover animation and interior instead of handling mouse drags.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_DraggingBounds")]
    static extern void SetDraggingBounds(GroupAttributes attributes, RectangleF bounds);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_uxDragBorder")]
    static extern ref Animated<float> DragAnimation(GroupAttributes attributes);

    static float HeaderHeight(GroupStyle style) => style.Appearance.ShowTitle
        ? 28 + (float)style.Appearance.ExtraHeaderSpace : 0;

    // The live canvas calls a non-virtual cached Layout overload and infers nested
    // bounds privately. Finalise child frames before their parents are painted.
    void FitMembers(GroupStyle style, long tick)
    {
        if (tick != 0 && frameTick == tick) return;
        frameTick = tick;
        var members = new RectangleF(Pivot, SizeF.Empty);
        foreach (var member in group.Content)
        {
            if (member.Attributes is StyledGroup child)
                child.FitMembers(Groups.Style(child.group), tick);
            var bounds = member.Attributes.AggregateBounds;
            members = members.IsEmpty ? bounds : RectangleF.Union(members, bounds);
        }
        // Match GH2's native member/pin padding and empty-group width.
        var frame = members.AdjustSides(members.Width == 0 ? 42 : 14, 14);
        float header = style.Designed && !failed ? HeaderHeight(style)
            : string.IsNullOrWhiteSpace(group.DisplayName) ? 0 : 18;
        Bounds = frame.AdjustSides(0, header, 0, 0);
        PinAttributes.LayoutPins(group);
    }

    public override bool HandleDoubleClick(PointF point)
    {
        var title = TitleRegion;
        if (!title.IsEmpty && title.Contains(point))
        {
            Eto.Forms.Application.Instance.AsyncInvoke(() => GroupEditor.Show(group));
            return true;
        }
        return base.HandleDoubleClick(point);
    }
    public override void Layout(Shape shape)
    {
        base.Layout(shape);
        FitMembers(Groups.Style(group), 0);
    }

    Color BackdropAt(PointF point, Skin skin)
    {
        var background = skin.Canvas.Background;
        if (group.Document is not { } document) return background;
        // Groups are painted in native z-order. An inner heading can sit over
        // its parent's translucent fill even when both titles use automatic ink.
        foreach (var behind in document.Objects.Groups)
        {
            if (ReferenceEquals(behind, group)) break;
            if (!behind.Attributes.Bounds.Contains(point)) continue;
            if (behind.Attributes is StyledGroup styled)
                background = Color.Blend(background, styled.bandBounds.Contains(point) ? styled.paintedBand : styled.paintedFill);
        }
        return background;
    }

    public override void Draw(Context context, Skin skin)
    {
        var style = Groups.Style(group);
        FitMembers(style, context.Control?.DrawStartTime.Ticks ?? 0);
        TitleRegion = RectangleF.Empty;
        bandBounds = RectangleF.Empty;
        if (failed || !style.Designed)
        {
            paintedFill = (group.Selected ? skin.Shades[ShadeKind.NormalSelected].Apex
                : OpenColor.Create(group.GroupColour, 3)).WithA(.7f);
            base.Draw(context, skin);
            return;
        }
        try
        {
            var frame = Bounds;
            var appearance = style.Appearance;
            var g = context.Graphics;
            var above = PinAttributes.DrawPinsAbove(context, skin, group);
            var below = PinAttributes.DrawPinsBelow(context, skin, group);
            var corners = new CornerRadii((float)Math.Min(appearance.Radius, Math.Min(frame.Width, frame.Height) / 2));
            if (above > 0) corners = corners.WithRadii(0f, null, 0f);
            if (below > 0) corners = corners.WithRadii(null, 0f, null, 0f);
            using var path = frame.ToRoundedRectangle(corners);
            paintedFill = Color.Parse(style.Fill).WithA((float)(style.Opacity / 100));
            g.FillPath(paintedFill, path);

            var header = HeaderHeight(style);
            var textOrigin = new PointF(frame.X + 8, frame.Y + 4);
            var background = BackdropAt(textOrigin, skin).ToHex(false);
            var resolved = GroupAppearancePolicy.Resolve(appearance, style.Fill, style.Opacity, background);
            var accent = Color.Parse(resolved.Accent);
            if (resolved.Band && header > 0)
            {
                bandBounds = new RectangleF(frame.X, frame.Y, frame.Width, header);
                paintedBand = accent;
                using var band = bandBounds.ToRoundedRectangle(corners.WithRadii(null, 0f, null, 0f));
                g.FillPath(accent, band);
            }

            // A press in the gap between separated members starts a native window
            // selection. Hovering the edge or heading exposes the group drag border.
            var hover = context.Animate(DragAnimation(this));
            var inset = -12 * hover;
            var interior = frame.AdjustSides(inset, inset - header * hover, inset, inset);
            SetDraggingBounds(this, interior);
            if (hover > 0 && !interior.IsEmpty)
            {
                using var edge = interior.ToRoundedRectangle(0);
                g.DrawPath(accent.WithA(.5f * hover), edge);
            }
            if (appearance.ShowAccent)
                g.FillRectangle(accent, frame.X + 1, frame.Y + 8, 3, frame.Height - 16);
            if (appearance.BorderWidth > 0 || group.Selected)
            {
                var border = group.Selected ? skin.Shades[ShadeKind.NormalSelected].Edge : Color.Parse(resolved.Border);
                var weight = (float)appearance.BorderWidth;
                using var pen = new Pen(border, group.Selected ? Math.Max(2.5f, weight) : weight);
                g.DrawPath(pen, path);
            }
            if (header == 0) return;
#if MARGIN_MAC
            const float scale = 1;
#else
            const float scale = .75f;
#endif
            using var font = new Font(SystemFonts.Default().Family, (float)appearance.TitleSize * scale, appearance.Bold ? FontStyle.Bold : FontStyle.None);
            var name = GroupAppearancePolicy.FormatTitle(string.IsNullOrWhiteSpace(group.UserName) ? "Group" : group.UserName, appearance.TitleCase);
            var available = frame.Width - 16;
            while (name.Length > 1 && g.MeasureString(font, name).Width > available)
                name = name[..^2] + "…";
            var size = g.MeasureString(font, name);
            TitleRegion = new RectangleF(textOrigin, size);
            g.DrawText(font, Color.Parse(resolved.Text), textOrigin, name);
            if (resolved.Divider)
                g.DrawLine(accent, frame.Left + 8, frame.Top + header - 1, Math.Min(frame.Right - 8, frame.Left + 170), frame.Top + header - 1);
        }
        catch (Exception ex)
        {
            failed = true;
            TitleRegion = RectangleF.Empty;
            Rhino.RhinoApp.WriteLine("Margin group styling paused: " + ex.Message);
            base.Draw(context, skin);
        }
    }
}
#else
public sealed class StyledGroup : GH_GroupAttributes, Grasshopper.Kernel.IGH_Attributes
{
    readonly GH_Group group;
    bool failed;
    RectangleF[] frameContents = [];
    GroupAppearance? layoutAppearance;
    GH_GroupBorder? layoutBorder;
    GraphicsPath? outline;
    RectangleF frame, title;
    public StyledGroup(GH_Group group) : base(group) => this.group = group;
    RectangleF Frame()
    {
        var contents = group.ContentBoxes()?.ToArray() ?? [];
        var appearance = Groups.Style(group).Appearance;
        if (contents.SequenceEqual(frameContents) && appearance == layoutAppearance && group.Border == layoutBorder)
            return frame;
        base.ExpireLayout();
        outline?.Dispose(); outline = null;
        frameContents = contents; layoutAppearance = appearance; layoutBorder = group.Border;
        frame = title = RectangleF.Empty;
        if (contents.Length == 0) return frame;
        var b = contents.Aggregate(RectangleF.Union);
        switch (group.Border)
        {
            case GH_GroupBorder.Box:
                b.Inflate(22, 22);
                if (appearance.ShowTitle)
                {
                    var header = 85 + (float)appearance.ExtraHeaderSpace;
                    b.Y -= header; b.Height += header; b.Width = Math.Max(180, b.Width);
                    title = TitleBounds(b, appearance);
                }
                outline = GroupFrame.Path(b.X, b.Y, b.Width, b.Height, (float)appearance.Radius);
                break;
            case GH_GroupBorder.Blob:
                outline = Grasshopper.GUI.GH_GDI_Util.Freeform_Blob(contents, 20, 2.0);
                break;
            case GH_GroupBorder.Rectangles:
                var boxes = contents.Select(c => { var box = Grasshopper.Kernel.GH_Convert.ToRectangle(c); box.Inflate(30, 30); return box; }).ToList();
                if (Grasshopper.GUI.GH_GDI_Util.BoxUnion(boxes) is not null)
                    outline = Grasshopper.GUI.GH_GDI_Util.FilletBoxOutline(boxes, 20);
                break;
        }
        if (group.Border != GH_GroupBorder.Box)
        {
            // Keep the native contour intact. Freeform and borderless groups get a
            // heading above it, without a rectangular band or replacement frame.
            if (outline is not null) b = outline.GetBounds();
            if (appearance.ShowTitle)
            {
                var height = (float)appearance.TitleSize * 1.5f + 7;
                title = new(b.X, b.Y - height - 12, Math.Max(180, b.Width), height);
            }
        }
        frame = title.IsEmpty ? b : RectangleF.Union(b, title);
        return frame;
    }
    internal RectangleF TitleRegion { get { Frame(); return title; } }
    static RectangleF TitleBounds(RectangleF b, GroupAppearance appearance) => new(b.X+18,b.Y+10,b.Width-34,(float)appearance.TitleSize*1.5f+7);
    public new RectangleF Bounds { get => Groups.Style(group).Designed ? Frame() : base.Bounds; set => base.Bounds = value; }
    public new bool IsPickRegion(PointF point)
    {
        if (!Groups.Style(group).Designed) return base.IsPickRegion(point);
        Frame();
        return title.Contains(point) || (outline?.IsVisible(point) ?? false);
    }
    public new bool IsPickRegion(RectangleF rectangle, Grasshopper.Kernel.GH_PickBox pick)
    {
        if (!Groups.Style(group).Designed) return base.IsPickRegion(rectangle, pick);
        Frame();
        if (frame.IsEmpty) return false;
        if (pick == Grasshopper.Kernel.GH_PickBox.Window) return rectangle.Contains(frame);
        if (pick != Grasshopper.Kernel.GH_PickBox.Crossing) return false;
        return (!title.IsEmpty && title.IntersectsWith(rectangle)) || (outline is not null &&
            Grasshopper.Kernel.GH_Attributes<GH_Group>.SolvePathBoxPick(outline, rectangle, 1f, pick));
    }
    public new bool IsMenuRegion(PointF point) => IsPickRegion(point);
    bool IGH_TooltipAwareObject.IsTooltipRegion(PointF point) => IsPickRegion(point);
    bool TitleContains(PointF point) { Frame(); return title.Contains(point); }
    public new GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, Grasshopper.GUI.GH_CanvasMouseEvent e)
    {
        if (Groups.Style(group).Designed && TitleContains(e.CanvasLocation))
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
            var boxed = group.Border == GH_GroupBorder.Box;
            if (outline is not null)
            {
                using var fill = new SolidBrush(colour); g.FillPath(fill, outline);
                if (boxed && resolved.Band)
                {
                    using var band = GroupFrame.Path(b.X,b.Y,b.Width,(float)appearance.TitleSize*1.3f+24,(float)appearance.Radius,true);
                    using var bandFill = new SolidBrush(accentColour); g.FillPath(bandFill,band);
                }
                if (appearance.BorderWidth > 0 || Selected)
                {
                    using var pen = new Pen(borderColour, Selected ? Math.Max(2.5f,(float)appearance.BorderWidth) : (float)appearance.BorderWidth);
                    g.DrawPath(pen,outline);
                }
                if (boxed && appearance.ShowAccent)
                { using var accent = new SolidBrush(accentColour);g.FillRectangle(accent,b.X+1,b.Y+10,4,b.Height-20); }
            }
            if (title.IsEmpty) return;
            using var font = new Font(SystemFonts.DefaultFont.FontFamily,(float)appearance.TitleSize,appearance.Bold ? FontStyle.Bold : FontStyle.Regular,GraphicsUnit.Pixel);
            string name = string.IsNullOrWhiteSpace(group.NickName)?"Group":group.NickName;
            name = GroupAppearancePolicy.FormatTitle(name, appearance.TitleCase);
            // Freeform headings sit on the canvas, not on the group's fill/band.
            var titleInk = Groups.TitleColour(group, appearance, style.Fill, colour.A / 255d * 100, canvasColour);
            var ink = ColorTranslator.FromHtml(titleInk);
            using var text = new SolidBrush(ink);
            using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var titleBounds = title;
            g.DrawString(name,font,text,titleBounds,format);
            if (Selected && group.Border == GH_GroupBorder.None)
            {
                using var selection = new Pen(ink, 1.5f);
                g.DrawLine(selection, title.Left, title.Bottom - 2, title.Left + Math.Min(title.Width, 170), title.Bottom - 2);
            }
            if (boxed && resolved.Divider)
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
