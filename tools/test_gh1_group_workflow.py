"""RhinoCode regression: local copy/paste, pick, rename, move, ungroup, undo/reopen.

Uses disposable documents, never assigns Instances.ActiveCanvas.Document and
never touches the system clipboard. Optional temp/margin-group-workflow-config.json
selects a separately named QA assembly: {"namespace": "MarginQa.Native",
"assemblies": ["/absolute/Core.dll", "/absolute/Native.dll"], "run": "qa"}.
"""
import importlib
from contextlib import contextmanager
import json
import tempfile
import traceback
from pathlib import Path
from urllib.parse import unquote, urlparse
import System
import Rhino
import clr

SCRIPT = Path(unquote(urlparse(__file__).path) if __file__.startswith('file:') else __file__)
OUTPUT = SCRIPT.resolve().parents[1] / 'artifacts' / 'group-workflow'


def verify_selected_group_copy(Groups, GroupStyle, check):
    from Grasshopper.Kernel import GH_Document, GH_DocumentIO, GH_ClipboardType
    from Grasshopper.Kernel.Parameters import Param_Curve
    from Grasshopper.Kernel.Special import GH_Group, GH_GroupBorder
    from Grasshopper.GUI import GH_CanvasMouseEvent
    from Grasshopper.GUI.Canvas import GH_Canvas
    from Grasshopper.GUI.Canvas.Interaction import GH_DragInteraction, GH_WindowSelectInteraction
    from itertools import product
    from System.Drawing import Point, PointF, Size
    from System.Windows.Forms import MouseButtons, Keys

    def observe(canvas, name, point):
        screen = canvas.Viewport.ProjectPoint(point)
        event = GH_CanvasMouseEvent(Point(int(screen.X), int(screen.Y)), point, MouseButtons.Left, 1, 0)
        method = clr.GetClrType(Groups).GetMethod(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        method.Invoke(None, System.Array[System.Object]([canvas, event.WinFormsEventArgs]))
        return event

    def click_group(canvas, group, point, modifiers=System.Enum.ToObject(Keys, 0)):
        # Run native picking and drag setup, then Margin's post-selection handler.
        # Explicit modifiers keep background QA independent of the user's keyboard.
        canvas.Document.Select(canvas.Document.RelevantObjectAtPoint(point), modifiers == Keys.Shift, modifiers == Keys.Control)
        event = GH_CanvasMouseEvent(Point(int(point.X), int(point.Y)), point, MouseButtons.Left, 1, 0)
        canvas.ActiveInteraction = GH_DragInteraction(canvas, event) if canvas.Document.SelectedCount else None
        observe(canvas, 'CanvasMouseDown', point)
        return canvas.ActiveInteraction

    def select_rectangle(canvas, start, end, mode='Replace'):
        event = lambda p: GH_CanvasMouseEvent(Point(int(p.X), int(p.Y)), p, MouseButtons.Left, 1, 0)
        interaction = GH_WindowSelectInteraction(canvas, event(start))
        canvas.ActiveInteraction = interaction
        observe(canvas, 'CanvasMouseDown', start)
        interaction.RespondToMouseMove(canvas, event(end))
        # Select Replace explicitly; never change the user's physical modifiers.
        field = interaction.GetType().GetField('m_SelType', System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        field.SetValue(interaction, System.Enum.Parse(field.FieldType, mode))
        interaction.RespondToMouseUp(canvas, event(end))
        canvas.ActiveInteraction = None
        observe(canvas, 'CanvasMouseUp', end)

    # Exercise the selection-limited Copy command, not a whole-document clone.
    # The local clipboard uses the same serializer without touching the user's clipboard.
    for cross_document in (False, True):
        for nested, crossing in product((False, True), repeat=2):
            source = GH_Document()
            destination = GH_Document() if cross_document else source
            canvas = GH_Canvas()
            canvas.Size = Size(1000, 800)
            copied = None
            drag = None
            label = ('cross-definition' if cross_document else 'same-definition') + (' nested' if nested else '') + (' title crossing' if crossing else ' frame click')
            try:
                Groups.Attach(source)
                Groups.Attach(destination)
                members = []
                for x, y in ((200, 280), (450, 400)):
                    member = Param_Curve()
                    member.CreateAttributes()
                    member.Attributes.Pivot = PointF(x, y)
                    source.AddObject(member, False)
                    member.Attributes.PerformLayout()
                    members.append(member)
                group = GH_Group()
                group.Border = GH_GroupBorder.Box
                for member in members: group.AddObject(member.InstanceGuid)
                source.AddObject(group, False)
                if nested:
                    outer = GH_Group()
                    outer.Border = GH_GroupBorder.Box
                    outer.AddObject(group.InstanceGuid)
                    source.AddObject(outer, False)
                    group = outer
                snapshot = Groups.Snapshot(source)
                entry = next(item for item in snapshot.Items if item.Id == str(group.InstanceGuid))
                Groups.BeginEdit(source, snapshot.Document, entry.Id, entry.Revision).Save('Copy me', GroupStyle('#6937C4', 96, True))
                source.DeselectAll()
                bounds = group.Attributes.Bounds
                point = PointF(bounds.X + 35, bounds.Y + 20)
                canvas.Document = source
                if crossing:
                    select_rectangle(canvas, PointF(bounds.Right + 10, bounds.Top + 5), PointF(bounds.Left - 10, bounds.Top + 30))
                else:
                    drag = click_group(canvas, group, point)
                if drag is not None:
                    canvas.ActiveInteraction = None
                    drag = None
                before = [member.Attributes.Pivot for member in members]
                original_ids = {str(obj.InstanceGuid) for obj in source.Objects}
                io = GH_DocumentIO(source)
                check(io.Copy(GH_ClipboardType.Local, True) and io.Paste(GH_ClipboardType.Local), label + ': native selection copy/paste succeeds')
                copied = io.Document
                check(copied.ObjectCount == (4 if nested else 3), label + ': copying the selected frame includes all contents')
                # Match the native Paste command, including disposing its temporary document.
                copied.TranslateObjects(Size(10, 10), False)
                copied.SelectAll()
                copied.ExpireSolution()
                copied.MutateAllIds()
                new_objects = list(copied.Objects)
                destination.DeselectAll()
                destination.MergeDocument(copied)
                copied.Dispose()
                copied = None
                destination.NewSolution(False)
                pasted_members = [obj for obj in new_objects if isinstance(obj, Param_Curve)]
                pasted_groups = [obj for obj in new_objects if isinstance(obj, GH_Group)]
                new_ids = {str(obj.InstanceGuid) for obj in new_objects}
                check(not new_ids.intersection(original_ids) and all(str(id_) in new_ids for g in pasted_groups for id_ in g.ObjectIDs), label + ': pasted membership refers only to new objects')
                pasted = next(g for g in pasted_groups if g.NickName == 'Copy me')
                destination.DeselectAll()
                bounds = pasted.Attributes.Bounds
                point = PointF(bounds.X + 35, bounds.Y + 20)
                canvas.Document = destination
                event = lambda p: GH_CanvasMouseEvent(Point(int(p.X), int(p.Y)), p, MouseButtons.Left, 1, 0)
                start = [member.Attributes.Pivot for member in pasted_members]
                drag = click_group(canvas, pasted, point)
                target = PointF(point.X + 80, point.Y)
                drag.RespondToMouseMove(canvas, event(target))
                drag.RespondToMouseUp(canvas, event(target))
                check(all(abs(member.Attributes.Pivot.X - p.X - 80) < .1 and abs(member.Attributes.Pivot.Y - p.Y) < .1 for member, p in zip(pasted_members, start)), label + ': native drag moves every pasted member once')
                check(before == [member.Attributes.Pivot for member in members], label + ': dragging the copy leaves the originals untouched')
                destination.UndoServer.PerformUndo()
                check(start == [member.Attributes.Pivot for member in pasted_members], label + ': one undo restores all pasted members')
                canvas.ActiveInteraction = None
                drag = None
                click_group(canvas, pasted, point, Keys.Control)
                check(destination.SelectedCount == 0 and canvas.ActiveInteraction is None, label + ': control-click deselects the complete group and cancels its drag')
                box = pasted_members[0].Attributes.Bounds
                select_rectangle(canvas, PointF(box.Left - 2, box.Top - 2), PointF(box.Right + 2, box.Bottom + 2))
                check(pasted_members[0].Attributes.Selected and not pasted_members[1].Attributes.Selected
                      and all(not frame.Attributes.Selected for frame in pasted_groups),
                      label + ': window selection can select one member independently')
                click_group(canvas, pasted, point, Keys.Shift)
                check(all(obj.Attributes.Selected for obj in new_objects), label + ': shift-click adds the complete group')
                canvas.ActiveInteraction = None
                select_rectangle(canvas, PointF(box.Left - 2, box.Top - 2), PointF(box.Right + 2, box.Bottom + 2), 'RemoveFrom')
                check(not pasted_members[0].Attributes.Selected and pasted_members[1].Attributes.Selected
                      and pasted.Attributes.Selected, label + ': control-window removes one member without reselecting it')
                # Native inversion must not reselect a member as a side effect of
                # selecting its frame later in the same pass.
                destination.Select(destination.RelevantObjectAtPoint(pasted_members[0].Attributes.Pivot), False, False)
                selected = {str(obj.InstanceGuid): obj.Attributes.Selected for obj in destination.Objects}
                destination.InvertSelection()
                check(all(obj.Attributes.Selected != selected[str(obj.InstanceGuid)] for obj in destination.Objects),
                      label + ': invert selection toggles every object independently')
            finally:
                canvas.ActiveInteraction = None
                canvas.Document = None
                canvas.Dispose()
                if copied is not None: copied.Dispose()
                if destination is not source: destination.Dispose()
                source.Dispose()


def verify_outlines(Groups, GroupStyle, check):
    from Grasshopper.Kernel import GH_Document, GH_PickBox
    from Grasshopper.Kernel.Parameters import Param_Curve
    from Grasshopper.Kernel.Special import GH_Group, GH_GroupAttributes, GH_GroupBorder
    from System.Drawing import PointF, RectangleF
    d = GH_Document()
    try:
        Groups.Attach(d)
        members = []
        for x, y in [(200, 280), (500, 500)]:
            member = Param_Curve(); member.CreateAttributes(); d.AddObject(member, False)
            member.Attributes.Pivot = PointF(x, y); member.Attributes.ExpireLayout(); member.Attributes.PerformLayout()
            members.append(member)
        group = GH_Group()
        for member in members: group.AddObject(member.InstanceGuid)
        d.AddObject(group, False)
        snapshot = Groups.Snapshot(d); entry = snapshot.Items[0]
        Groups.BeginEdit(d, snapshot.Document, entry.Id, entry.Revision).Save('', GroupStyle('#253E56', 85, True))
        for border in [GH_GroupBorder.Blob, GH_GroupBorder.Rectangles, GH_GroupBorder.Box, System.Enum.Parse(GH_GroupBorder, 'None')]:
            group.Border = border
            check(Groups.Snapshot(d).Items[0].Outline == str(border), str(border)+' outline reaches Studio')
            group.Attributes.ExpireLayout()
            native = GH_GroupAttributes(group)
            b = native.Bounds
            if border in [GH_GroupBorder.Blob, GH_GroupBorder.Rectangles]:
                points = [PointF(b.X + b.Width * x / 10, b.Y + b.Height * y / 10) for x in range(1,10) for y in range(1,10)]
                check(all(group.Attributes.IsPickRegion(p) == native.IsPickRegion(p) for p in points), str(border)+' picking follows the native contour')
                gap = next(p for p in points if not native.IsPickRegion(p))
                check(not group.Attributes.IsMenuRegion(gap), str(border)+' has no invisible rectangular context-menu region')
                box = RectangleF(gap.X, gap.Y, .1, .1)
                check(not group.Attributes.IsPickRegion(box, GH_PickBox.Crossing), str(border)+' crossing selection excludes gaps')
            elif border == System.Enum.Parse(GH_GroupBorder, 'None'):
                check(not group.Attributes.IsPickRegion(PointF(350,390)), 'None does not leave an enclosing pick region')
            check(all(group.Attributes.Bounds.Contains(member.Attributes.Bounds) for member in members), str(border)+' bounds contain members')
        before = group.Attributes.Bounds
        members[1].Attributes.Pivot = PointF(650,600); members[1].Attributes.ExpireLayout(); members[1].Attributes.PerformLayout()
        check(group.Attributes.Bounds != before, 'outline layout follows moved members before painting')
    finally:
        d.Dispose()


def verify_cross_definition(Groups, GroupStyle, source, original, check):
    from Grasshopper.Kernel import GH_Document, GH_DocumentIO
    from Grasshopper.Kernel.Special import GH_Group, GH_GroupBorder
    destination = GH_Document()
    duplicate = reopened = None
    try:
        Groups.Attach(destination)
        outer = GH_Group(); outer.NickName = ''; outer.AddObject(original.InstanceGuid)
        source.AddObject(outer, False)
        snapshot = Groups.Snapshot(source); entry = next(e for e in snapshot.Items if e.Id == str(outer.InstanceGuid))
        other = GroupStyle.Parse(json.dumps({'Fill':'#117755','Opacity':80,'Designed':True,
            'Appearance':{'ShowTitle':False,'TitleCase':'Lowercase','Text':'#FFFFFF','TitleSize':19,'Heading':'Plain'}}))
        Groups.BeginEdit(source, snapshot.Document, entry.Id, entry.Revision).Save('', other)
        original.Border = GH_GroupBorder.Blob
        members = [o for o in source.Objects if not isinstance(o, GH_Group)]
        for index, member in enumerate(members): member.NickName = 'Member '+str(index)
        same_name = GH_Group(); same_name.NickName = original.NickName; same_name.AddObject(members[-1].InstanceGuid)
        source.AddObject(same_name, False)
        snapshot = Groups.Snapshot(source); entry = next(e for e in snapshot.Items if e.Id == str(same_name.InstanceGuid))
        Groups.BeginEdit(source, snapshot.Document, entry.Id, entry.Revision).Save(same_name.NickName, other)
        def signature(document):
            def describe(obj):
                if not isinstance(obj, GH_Group): return (obj.GetType().FullName, obj.NickName)
                children = tuple(sorted(describe(document.FindObject(id, True)) for id in obj.ObjectIDs))
                return ('Group', obj.NickName, str(obj.Border), Groups.Style(obj).ToString(), children)
            return sorted(describe(g) for g in document.Objects if isinstance(g, GH_Group))
        expected = signature(source)
        check(not Groups.Style(original).Equals(Groups.Defaults), 'copy fixture differs from destination future defaults')
        duplicate = GH_Document.DuplicateDocument(source); duplicate.MutateAllIds()
        destination.MergeDocument(duplicate); Groups.Attach(destination)
        check(signature(destination) == expected, 'cross-definition copy preserves same-named and nested group appearances, outlines and member relationships')
        original_member_name = source.FindObject(original.ObjectIDs[0], True).NickName
        copied = next(g for g in destination.Objects if isinstance(g, GH_Group) and g.NickName == original.NickName
                      and destination.FindObject(g.ObjectIDs[0], True).NickName == original_member_name)
        original_style = Groups.Style(original)
        snapshot = Groups.Snapshot(destination); entry = next(e for e in snapshot.Items if e.Id == str(copied.InstanceGuid))
        Groups.BeginEdit(destination, snapshot.Document, entry.Id, entry.Revision).Save(copied.NickName, other)
        check(Groups.Style(original).Equals(original_style) and Groups.Style(copied).Appearance.Equals(other.Appearance), 'editing a cross-definition copy leaves the original appearance unchanged')
        destination.UndoServer.PerformUndo()
        check(Groups.Style(copied).Equals(original_style), 'copy appearance edit undo restores transferred design')
        destination.UndoServer.PerformRedo()
        check(Groups.Style(copied).Appearance.Equals(other.Appearance), 'copy appearance edit redo restores independent design')
        snapshot = Groups.Snapshot(source); entry = next(e for e in snapshot.Items if e.Id == str(original.InstanceGuid))
        Groups.BeginEdit(source, snapshot.Document, entry.Id, entry.Revision).Save(original.NickName, GroupStyle('#991133', 60, True))
        check(Groups.Style(copied).Appearance.Equals(other.Appearance), 'editing the original leaves its copy unchanged')
        expected = signature(destination)
        path = str(OUTPUT / 'Cross-definition-copy.gh')
        check(GH_DocumentIO(destination).SaveQuiet(path), 'cross-definition destination saves')
        reopened = GH_DocumentIO(); check(reopened.Open(path), 'cross-definition destination reopens')
        Groups.Attach(reopened.Document)
        check(signature(reopened.Document) == expected, 'all destination group appearances and outlines survive reopen')
        check(Groups.Style(next(g for g in reopened.Document.Objects if isinstance(g, GH_Group) and g.NickName == '')).Appearance.Equals(other.Appearance), 'unnamed nested group retains its distinct appearance')
    finally:
        if reopened is not None and reopened.Document is not None: reopened.Document.Dispose()
        if duplicate is not None: duplicate.Dispose()
        destination.Dispose()


def verify_legacy(Groups, GroupStyle, check):
    from Grasshopper.Kernel import GH_Document
    from Grasshopper.Kernel.Special import GH_Group
    from Grasshopper.Kernel.Parameters import Param_Curve
    from System.Drawing import Color
    d = GH_Document(); duplicate = None
    try:
        member=Param_Curve();member.CreateAttributes();d.AddObject(member,False)
        group=GH_Group();group.AddObject(member.InstanceGuid);group.Colour=Color.FromArgb(204,17,119,85);d.AddObject(group,False)
        legacy=json.dumps({'Fill':'#117755','Opacity':80,'Designed':True,'Appearance':{'Text':'#FFCC44','TitleCase':'Uppercase','TitleSize':26}})
        # Fixture in the pre-portable format; observe behaviour only through the native group workflow.
        d.ValueTable.SetValue('Margin.GroupStyle.'+group.InstanceGuid.ToString('N'),legacy)
        Groups.Attach(d)
        check(Groups.Style(group).Appearance.ShowTitle and Groups.Style(group).Appearance.Equals(GroupStyle.Parse(legacy).Appearance), 'existing definition keeps its visible title and original appearance')
        duplicate=GH_Document.DuplicateDocument(d);duplicate.MutateAllIds();Groups.Attach(duplicate)
        copied=next(g for g in duplicate.Objects if isinstance(g,GH_Group))
        check(Groups.Style(copied).Equals(Groups.Style(group)), 'legacy appearance survives first copy without an intervening edit')
    finally:
        if duplicate is not None: duplicate.Dispose()
        d.Dispose()


@contextmanager
def distinctive_defaults(Groups, GroupStyle, use_group_colour=True, show_title=True, designed=True):
    # Swap only in-memory test state on the UI thread; never write user preferences.
    field = clr.GetClrType(Groups).GetField('preferences', System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
    original = field.GetValue(None)
    sentinel = System.Activator.CreateInstance(field.FieldType)
    style = GroupStyle.Parse(json.dumps({'Fill':'#742196','Opacity':72,'Designed':designed,
        'Appearance':{'ShowTitle':show_title,'TitleSize':29,'Text':'#FFCA31','TitleCase':'Lowercase','Heading':'Band'}}))
    for name, value in [('GroupFill',style.Fill),('GroupOpacity',style.Opacity),('StyleGroups',designed),
                        ('UseGroupColour',use_group_colour),('GroupAppearance',style.Appearance)]:
        field.FieldType.GetProperty(name).SetValue(sentinel, value)
    field.SetValue(None, sentinel)
    try:
        yield
    finally:
        field.SetValue(None, original)


def verify_native_defaults(Groups, check, use_group_colour=True):
    from Grasshopper.Kernel import GH_Document
    from Grasshopper.Kernel.Special import GH_Group
    from Grasshopper.Kernel.Parameters import Param_Curve
    from Grasshopper.GUI.Canvas import GH_Skin
    d = GH_Document()
    try:
        Groups.Attach(d)
        member = Param_Curve(); member.CreateAttributes(); d.AddObject(member, False)
        # Grasshopper's Group command assigns the native colour BEFORE AddObject.
        group = GH_Group(); group.NickName = ''; group.Colour = GH_Skin.group_back
        group.AddObject(member.InstanceGuid); d.AddObject(group, False)
        actual = Groups.Style(group); expected = Groups.Defaults
        check(actual.Appearance.Equals(expected.Appearance) and actual.Designed == expected.Designed,
              'native Group command colour initialization does not suppress saved appearance defaults')
        studio_group = GH_Group(); studio_group.AddObject(member.InstanceGuid); d.AddObject(studio_group, False)
        check(actual.Equals(Groups.Style(studio_group)),
              'native and Studio group creation receive the same defaults')
        from System.Drawing import Color, ColorTranslator
        expected_colour = Color.FromArgb(int(System.Math.Round(expected.Opacity * 2.55)), ColorTranslator.FromHtml(expected.Fill)) if use_group_colour else GH_Skin.group_back
        check(group.Colour.ToArgb() == expected_colour.ToArgb(), 'new group colour respects UseGroupColour='+str(use_group_colour))
        explicit = GH_Group(); explicit.Colour = Color.FromArgb(190, 18, 134, 76)
        explicit.AddObject(member.InstanceGuid); original = explicit.Colour; d.AddObject(explicit, False)
        check(explicit.Colour.ToArgb() == original.ToArgb(), 'explicit custom native colour is preserved')
    finally:
        d.Dispose()


def verify_hidden_default_reopens(Groups, check):
    from Grasshopper.Kernel import GH_Document, GH_DocumentIO
    from Grasshopper.Kernel.Special import GH_Group, GH_GroupAttributes
    from Grasshopper.Kernel.Parameters import Param_Curve
    from Grasshopper.GUI.Canvas import GH_Canvas, GH_CanvasChannel, GH_Skin
    from System.Drawing import Bitmap, Color, Graphics, Point, PointF, Size
    from System.Drawing.Imaging import ImageFormat
    from System.IO import MemoryStream
    document = GH_Document()
    reopened = GH_DocumentIO()
    try:
        Groups.Attach(document)
        member = Param_Curve()
        member.CreateAttributes()
        document.AddObject(member, False)
        member.Attributes.Pivot = PointF(150, 110)
        member.Attributes.ExpireLayout()
        member.Attributes.PerformLayout()
        group = GH_Group()
        group.NickName = 'Native group with a hidden title'
        group.Colour = GH_Skin.group_back
        group.AddObject(member.InstanceGuid)
        document.AddObject(group, False)
        def hidden(g):
            style = Groups.Style(g)
            return not style.Designed and not style.Appearance.ShowTitle and g.NickName == 'Native group with a hidden title'
        check(hidden(group), 'new native GH1 group inherits a hidden title with native styling')
        # Supply a bitmap surface to a disposable GH canvas. A normal canvas sets
        # Graphics during its paint event; this one never needs an active document.
        canvas = GH_Canvas()
        try:
            canvas.Size = Size(320, 220)
            canvas.Viewport.Zoom = 1
            canvas.Viewport.Target = Point(0, 0)
            canvas.Viewport.ComputeProjection()
            target = clr.GetClrType(GH_Canvas).GetField('_graphics', System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            def render(attributes):
                bitmap = Bitmap(320, 220)
                graphics = Graphics.FromImage(bitmap)
                stream = MemoryStream()
                try:
                    graphics.Clear(Color.White)
                    target.SetValue(canvas, graphics)
                    attributes.RenderToCanvas(canvas, GH_CanvasChannel.Groups)
                    bitmap.Save(stream, ImageFormat.Png)
                    return bytes(stream.ToArray())
                finally:
                    target.SetValue(canvas, None)
                    stream.Dispose()
                    graphics.Dispose()
                    bitmap.Dispose()
            name = group.NickName
            named_frame = render(GH_GroupAttributes(group))
            group.NickName = ''
            native_frame = render(GH_GroupAttributes(group))
            group.NickName = name
            check(native_frame != named_frame, 'GH1 native named and blank renderings have distinct pixels')
            check(render(group.Attributes) == native_frame and group.NickName == name,
                  'hiding the native GH1 title removes only its balloon text and preserves the native frame pixels')
        finally:
            canvas.Dispose()
        path = str(OUTPUT / 'Hidden-native-default.gh')
        check(GH_DocumentIO(document).SaveQuiet(path) and reopened.Open(path), 'hidden native GH1 default fixture saves and reopens')
        Groups.Attach(reopened.Document)
        restored = reopened.Document.FindObject(group.InstanceGuid, True)
        check(hidden(restored) and restored.ObjectIDs.Contains(member.InstanceGuid)
              and restored.Attributes.Bounds == GH_GroupAttributes(restored).Bounds,
              'reopened native GH1 group keeps its hidden title, name, native frame and member')
    finally:
        if reopened.Document is not None:
            reopened.Document.Dispose()
        document.Dispose()


def verify_existing_native_copy(Groups, check):
    from Grasshopper.Kernel import GH_Document
    from Grasshopper.Kernel.Special import GH_Group
    from Grasshopper.Kernel.Parameters import Param_Curve
    from Grasshopper.GUI.Canvas import GH_Skin
    source = GH_Document(); destination = GH_Document(); duplicate = None
    try:
        member = Param_Curve(); member.CreateAttributes(); source.AddObject(member, False)
        group = GH_Group(); group.Colour = GH_Skin.group_back; group.AddObject(member.InstanceGuid)
        source.AddObject(group, False)
        original = Groups.Style(group)
        check(not original.Appearance.Equals(Groups.Defaults.Appearance), 'existing native copy fixture differs from future defaults')
        Groups.Attach(source); Groups.Attach(destination)
        check(Groups.Style(group).Equals(original), 'attaching an existing native group preserves its appearance')
        duplicate = GH_Document.DuplicateDocument(source); duplicate.MutateAllIds(); destination.MergeDocument(duplicate)
        copied = next(o for o in destination.Objects if isinstance(o, GH_Group))
        check(Groups.Style(copied).Equals(original), 'copying an existing native-colour group does not apply future defaults')
    finally:
        if duplicate is not None: duplicate.Dispose()
        destination.Dispose(); source.Dispose()


def run():
    report = {'checks': []}
    docs = []
    canvas = None
    current = None
    def check(value, name):
        report['checks'].append({'name': name, 'passed': bool(value)})
        assert value, name
    try:
        import Grasshopper
        from Grasshopper.Kernel import GH_Document, GH_DocumentIO
        from Grasshopper.Kernel.Parameters import Param_Curve
        from Grasshopper.Kernel.Special import GH_Group
        from System.Drawing import PointF, Size
        config_path = Path(tempfile.gettempdir()) / 'margin-group-workflow-config.json'
        config = json.loads(config_path.read_text()) if config_path.exists() else {}
        for assembly in config.get('assemblies', []):
            loaded = System.Reflection.Assembly.LoadFrom(assembly)
            clr.AddReference(loaded.FullName)
        native = importlib.import_module(config.get('namespace', 'Margin.Native'))
        Groups, GroupStyle = native.Groups, native.GroupStyle
        report['target'] = config.get('namespace', 'Margin.Native')
        report['run'] = config.get('run', 'installed')
        canvas = Grasshopper.Instances.ActiveCanvas
        current = canvas.Document if canvas else None
        ids = [str(o.InstanceGuid) for o in current.Objects] if current else []
        d = GH_Document(); docs.append(d); Groups.Attach(d)
        member = Param_Curve(); member.CreateAttributes(); d.AddObject(member, False)
        member.Attributes.Pivot = PointF(200, 280); member.Attributes.ExpireLayout(); member.Attributes.PerformLayout()
        group = GH_Group(); group.NickName = 'Original'; group.AddObject(member.InstanceGuid); d.AddObject(group, False)
        snapshot = Groups.Snapshot(d); entry = snapshot.Items[0]
        custom = GroupStyle.Parse(json.dumps({'Fill':'#6937C4','Opacity':96,'Designed':True,
            'Appearance':{'TitleSize':23,'Text':'#FFD080','TitleCase':'Uppercase','Heading':'Band','BorderWidth':3,'Radius':18}}))
        Groups.BeginEdit(d, snapshot.Document, entry.Id, entry.Revision).Save('Guide curves', custom)
        # DuplicateDocument uses native GH_DocumentIO.Copy/Paste with its LOCAL clipboard.
        duplicate = GH_Document.DuplicateDocument(d); docs.append(duplicate)
        duplicate.MutateAllIds(); d.MergeDocument(duplicate)
        pasted = next(o for o in d.Objects if isinstance(o, GH_Group) and o.InstanceGuid != group.InstanceGuid)
        pasted_id = pasted.InstanceGuid
        check(Groups.Style(pasted).Equals(Groups.Style(group)), 'same-definition copy preserves complete custom appearance')
        b = pasted.Attributes.Bounds
        hit = d.FindAttribute(PointF(b.X + 35, b.Y + 20), True)
        check(System.Object.ReferenceEquals(hit, pasted.Attributes), 'pasted heading picks the current group attributes')
        for o in d.Objects: o.Attributes.Selected = False
        hit.Selected = True
        check(any(o.InstanceGuid == pasted_id for o in d.SelectedObjects()), 'click selection reaches pasted group for native commands')
        check(hit.IsMenuRegion(PointF(b.X + 35, b.Y + 20)), 'pasted heading accepts its native context menu')
        snapshot = Groups.Snapshot(d); entry = next(e for e in snapshot.Items if e.Id == str(pasted_id))
        Groups.BeginEdit(d, snapshot.Document, entry.Id, entry.Revision).Save('Independent copy', entry.Style)
        check(pasted.NickName == 'Independent copy' and group.NickName == 'Guide curves', 'rename changes only the copied group')
        d.UndoServer.PerformUndo(); check(pasted.NickName == 'Guide curves', 'rename undo restores pasted name')
        d.UndoServer.PerformRedo(); check(pasted.NickName == 'Independent copy', 'rename redo restores edited pasted name')
        child = d.FindObject(pasted.ObjectIDs[0], True)
        for o in d.Objects: o.Attributes.Selected = o.InstanceGuid == child.InstanceGuid
        d.TranslateObjects(Size(100, 160), True)
        check(pasted.Attributes.Bounds.Contains(child.Attributes.Bounds), 'moved pasted member stays inside its frame')
        # Invoke the actual native group's Ungroup menu handler, bound to this doc.
        method = clr.GetClrType(GH_Group).GetMethod('Menu_Ungroup', System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        check(method is not None, 'native Ungroup menu handler is available')
        method.Invoke(pasted, System.Array[System.Object]([None, System.EventArgs.Empty]))
        check(d.FindObject(pasted_id, True) is None and d.FindObject(child.InstanceGuid, True) is not None,
              'native Ungroup removes pasted frame and preserves its member')
        check(d.FindObject(group.InstanceGuid, True) is not None, 'ungroup leaves original group intact')
        d.UndoServer.PerformUndo(); Groups.Attach(d)
        restored = d.FindObject(pasted_id, True)
        check(restored is not None and restored.ObjectIDs.Contains(child.InstanceGuid), 'ungroup undo restores pasted membership')
        b = restored.Attributes.Bounds
        check(System.Object.ReferenceEquals(d.FindAttribute(PointF(b.X + 35, b.Y + 20), True), restored.Attributes),
              'restored pasted heading remains clickable')
        OUTPUT.mkdir(parents=True, exist_ok=True)
        file = str(OUTPUT / 'Disposable-group-workflow.gh')
        io = GH_DocumentIO(d); check(io.SaveQuiet(file), 'fixture saves')
        reopened = GH_DocumentIO(); check(reopened.Open(file), 'fixture reopens'); docs.append(reopened.Document)
        Groups.Attach(reopened.Document)
        saved = reopened.Document.FindObject(pasted_id, True)
        check(Groups.Style(saved).Equals(Groups.Style(pasted)), 'copied custom appearance survives save and reopen')
        check(saved.NickName == 'Independent copy' and saved.ObjectIDs.Contains(child.InstanceGuid), 'pasted rename and membership survive reopen')
        b = saved.Attributes.Bounds
        check(System.Object.ReferenceEquals(reopened.Document.FindAttribute(PointF(b.X + 35, b.Y + 20), True), saved.Attributes),
              'reopened pasted group remains clickable')
        verify_selected_group_copy(Groups, GroupStyle, check)
        verify_outlines(Groups, GroupStyle, check)
        verify_cross_definition(Groups, GroupStyle, d, group, check)
        verify_legacy(Groups, GroupStyle, check)
        with distinctive_defaults(Groups, GroupStyle):
            verify_native_defaults(Groups, check)
            verify_existing_native_copy(Groups, check)
        with distinctive_defaults(Groups, GroupStyle, show_title=False, designed=False):
            verify_hidden_default_reopens(Groups, check)
        with distinctive_defaults(Groups, GroupStyle, use_group_colour=False):
            verify_native_defaults(Groups, check, use_group_colour=False)
        check(System.Object.ReferenceEquals(canvas.Document, current) and ids == [str(o.InstanceGuid) for o in current.Objects]
              if current else (canvas is None or canvas.Document is None), 'working canvas and object identities are untouched')
        report['passed'] = True
    except Exception:
        report['passed'] = False; report['error'] = traceback.format_exc()
    finally:
        for d in reversed(docs): d.Dispose()
        OUTPUT.mkdir(parents=True, exist_ok=True)
        (OUTPUT / (report.get('run', 'error') + '.json')).write_text(json.dumps(report, indent=2))

Rhino.RhinoApp.InvokeOnUiThread(System.Action(run))
