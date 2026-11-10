"""Render and pick isolated GH2 groups through the installed Mac host.

Run in RhinoCode with GH2 and Margin loaded. Creates an inactive document,
uses the two supplied GH2 skins and writes native bitmap evidence. Leaves the
active canvas, application skin, and saved Margin preferences alone.
"""
import json
import traceback
from pathlib import Path
from urllib.parse import unquote, urlparse

import Rhino
import System


def run():
    script = Path(unquote(urlparse(__file__).path) if __file__.startswith('file:') else __file__)
    output = script.resolve().parents[1] / 'artifacts' / 'gh2-groups' / 'outline'
    output.mkdir(parents=True, exist_ok=True)
    report = {'checks': []}
    doc = None

    def check(condition, description):
        report['checks'].append({'name': description, 'passed': bool(condition)})
        assert condition, description

    try:
        import clr
        from Eto.Forms import Control
        from Eto.Drawing import Bitmap, Graphics, ImageFormat, PixelFormat, PointF
        from Grasshopper2.Doc import Document, ObjectSelection
        from Grasshopper2.Eto.Drawing import Context
        from Grasshopper2.Parameters.Standard import NumberParameter
        from Grasshopper2.SpecialObjects import GroupObject
        from Grasshopper2.UI.Skinning import Skin
        from System import Array, Object
        from System.Reflection import BindingFlags

        assembly = next(a for a in System.AppDomain.CurrentDomain.GetAssemblies()
                        if not a.IsDynamic and a.GetName().Name == 'Margin.GH2')
        groups = assembly.GetType('Margin.Native.Groups', True)
        attach = next(m for m in groups.GetMethods() if m.Name == 'Attach' and len(m.GetParameters()) == 1)
        title_region = assembly.GetType('Margin.Native.StyledGroup', True).GetProperty(
            'TitleRegion', BindingFlags.Instance | BindingFlags.NonPublic)
        report['assembly'] = str(assembly.FullName)
        doc = Document.NewInactiveDocument()
        left, right = NumberParameter(), NumberParameter()
        for item, x in [(left, 160), (right, 450)]:
            doc.Objects.Add(item, PointF(x, 215))
        group = GroupObject()
        group.UserName = 'Nested components'
        for item in (left, right):
            group.AddContent(item.InstanceId)
        doc.Objects.Add(group)
        outer = GroupObject()
        outer.UserName = 'Outer group'
        outer.AddContent(group.InstanceId)
        doc.Objects.Add(outer)
        attach.Invoke(None, Array[Object]([doc]))

        # Seed just this disposable fixture, including a fully transparent rule title.
        def appearance(item, visible=True, text=None, fill='#FFFFFF', opacity=0, designed=True):
            data = {'Fill': fill, 'Opacity': opacity, 'Designed': designed,
                    'Appearance': {'ShowTitle': visible, 'Text': text, 'ShowAccent': False,
                                   'Heading': 'Plain', 'BorderWidth': 1, 'TitleSize': 17}}
            item.CustomValues.Set('Margin.GroupStyle', json.dumps(data))
            item.Attributes.InvalidateLayout()

        def render(skin, name):
            bitmap = Bitmap(650, 350, PixelFormat.Format32bppRgba)
            graphics = Graphics(bitmap)
            try:
                graphics.Clear(skin.Canvas.Background)
                context = Context.CreateFromBitmap(bitmap, graphics)
                for member in (left, right):
                    member.Attributes.Layout(skin.Shape)
                for frame in doc.Objects.Groups:
                    frame.Attributes.Layout(skin.Shape)
                    frame.Attributes.Draw(context, skin)
                for member in (left, right):
                    member.Attributes.Draw(context, skin)
            finally:
                graphics.Dispose()
            try:
                bitmap.Save(str(output / (name + '.png')), ImageFormat.Png)
                if name == 'selected':
                    # This edge lies wholly over the outer dark group on both sides.
                    x, y = int(group.Attributes.Bounds.Left), int(group.Attributes.Bounds.MiddleY)
                    def luminance(colour):
                        rgb = [v / 255 for v in (colour.Rb, colour.Gb, colour.Bb)]
                        linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in rgb]
                        return sum(w * v for w, v in zip((.2126, .7152, .0722), linear))
                    surface = luminance(bitmap.GetPixel(x - 6, y))
                    contrast = 0
                    for pixel in range(x - 3, x + 4):
                        edge = luminance(bitmap.GetPixel(pixel, y))
                        contrast = max(contrast, (max(edge, surface) + .05) / (min(edge, surface) + .05))
                    check(contrast >= 3, 'selected border contrasts with the nested dark surface')
                if name.startswith('native-'):
                    # Read the centre of the top of the frame, away from its text and edges.
                    b = group.Attributes.Bounds
                    sample = bitmap.GetPixel(int(b.Left + 8), int(b.Top + 6))
                    body = bitmap.GetPixel(int(b.Left + 8), int(b.MiddleY))
                    return sample.ToHex(False), body.ToHex(False)
                title = title_region.GetValue(group.Attributes)
                if title.IsEmpty:
                    return set()
                # Test the actual glyph pixels; antialiasing may slightly shift RGB.
                colours = set()
                for y in range(int(title.Top), int(title.Bottom)):
                    for x in range(int(title.Left), int(title.Right)):
                        colour = bitmap.GetPixel(x, y)
                        colours.add((int(colour.Rb), int(colour.Gb), int(colour.Bb)))
                return colours
            finally:
                bitmap.Dispose()

        def contains_ink(colours, expected):
            return any(max(abs(c - e) for c, e in zip(rgb, expected)) < 5 for rgb in colours)

        appearance(outer)
        appearance(group)
        for skin, name, ink in [(Skin.DefaultLit, 'light', (35, 40, 44)),
                                (Skin.DefaultDim, 'dark', (244, 243, 240))]:
            colours = render(skin, name)
            check(contains_ink(colours, ink), name + ' canvas resolves visible automatic title ink')
        appearance(outer, fill='#161B20', opacity=100)
        colours = render(Skin.DefaultLit, 'nested-dark-on-light')
        check(contains_ink(colours, (244, 243, 240)), 'inner transparent heading uses the enclosing dark fill')
        factory = assembly.GetType('Margin.Native.GroupEditor').GetMethod('CreateDialog', BindingFlags.Static | BindingFlags.NonPublic)
        def editor_ink():
            dialog = factory.Invoke(None, Array[Object]([group]))
            try:
                return dialog.FindChild(clr.GetClrType(Control), 'group-title-colour').Value.ToHex(False)
            finally:
                dialog.Dispose()
        check(editor_ink() == '#F4F3F0', 'editor swatch matches the light title painted on its enclosing dark group')
        appearance(outer, fill='#FFFFFF', opacity=100)
        render(Skin.DefaultDim, 'nested-light-on-dark')
        check(editor_ink() == '#23282C', 'editor swatch follows a light enclosing group on the dark canvas')
        appearance(outer, fill='#161B20', opacity=100)
        appearance(group, text='#C71585')
        colours = render(Skin.DefaultDim, 'custom-ink')
        check(contains_ink(colours, (199, 21, 133)), 'explicit title ink survives a dark canvas')
        b = group.Attributes.Bounds
        check(outer.Attributes.Bounds.Contains(b), 'outer rectangle encloses its styled child')
        gap = b.Center
        pick = doc.Objects.ResolvePick(gap, False, True, True, True)
        check(pick.ObjectUnderPick == group.InstanceId, 'right-click pick finds the inner native group in the gap')
        pick.UnsetIfNotOnGroupBorder()
        check(pick.ObjectUnderPick == System.Guid.Empty, 'primary interior press is available for native window selection')
        group.Selection = ObjectSelection.Selected
        group.Attributes.ShowDraggingBorder()
        # Let GH2's real hover feedback reach the draggable state before picking.
        System.Threading.Thread.Sleep(350)
        render(Skin.DefaultDim, 'selected')
        edge = PointF(b.Left + 3, b.MiddleY)
        edge_pick = doc.Objects.ResolvePick(edge, False, True, True, True)
        edge_pick.UnsetIfNotOnGroupBorder()
        check(group.Selected and edge_pick.ObjectUnderPick == group.InstanceId
              and group.Attributes.IsOnDraggingBorder(edge), 'selected native group retains its draggable edge')
        before = b.Right
        right.Attributes.Move(70, 15)
        render(Skin.DefaultLit, 'moved-member')
        check(group.Attributes.Bounds.Right >= before + 69, 'frame follows moved member')
        check(outer.Attributes.Bounds.Contains(group.Attributes.Bounds), 'outer frame follows the moved inner group')
        appearance(group, visible=False)
        prior = group.Attributes.Bounds
        render(Skin.DefaultLit, 'hidden-heading')
        check(title_region.GetValue(group.Attributes).IsEmpty, 'hidden heading has no label exclusion or edit region')
        check(group.Attributes.Bounds.Top > prior.Top and group.Attributes.Bounds.Bottom == prior.Bottom,
              'hiding the heading reclaims its space and keeps member padding')
        group.Selection = ObjectSelection.Unselected
        group.Attributes.HideDraggingBorder()
        System.Threading.Thread.Sleep(350)
        appearance(group, designed=False)
        visible = render(Skin.DefaultLit, 'native-visible')
        shown_bounds = group.Attributes.Bounds
        check(visible[0] != visible[1], 'visible native GH2 heading has its original coloured band')
        appearance(group, designed=False, visible=False)
        hidden = render(Skin.DefaultLit, 'native-hidden')
        hidden_bounds = group.Attributes.Bounds
        check(hidden[0] == hidden[1] == visible[1], 'hidden native GH2 heading leaves only the original frame fill')
        check(hidden_bounds.Top == shown_bounds.Top + 18 and hidden_bounds.Bottom == shown_bounds.Bottom
              and group.UserName == 'Nested components', 'native GH2 title space is reclaimed without renaming the group')
        dialog = factory.Invoke(None, Array[Object]([group]))
        try:
            show = dialog.FindChild(clr.GetClrType(Control), 'group-show-title')
            check(show.Enabled and not show.Checked, 'native GH2 editor can restore its hidden title')
            show.Checked = True
            dialog.DefaultButton.PerformClick()
        finally:
            dialog.Dispose()
        check(render(Skin.DefaultLit, 'native-restored') == visible and group.Attributes.Bounds == shown_bounds,
              'restoring the native title restores its original frame and heading colours')
        report['passed'] = True
    except Exception:
        report['passed'] = False
        report['error'] = traceback.format_exc()
    finally:
        if doc is not None:
            doc.Close()
        (output / 'result.json').write_text(json.dumps(report, indent=2))


Rhino.RhinoApp.InvokeOnUiThread(System.Action(run))
