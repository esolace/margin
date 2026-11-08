"""Run through RhinoCode in an empty Margin-QA.3dm session; never on coursework.

Creates its own GH definition, exercises the native translation operation, and
restores the previous active definition. Writes evidence under artifacts/.
"""
import json
import traceback
from pathlib import Path
from urllib.parse import unquote, urlparse

import System
import Rhino

SCRIPT = Path(unquote(urlparse(__file__).path) if __file__.startswith('file:') else __file__)
OUTPUT = SCRIPT.resolve().parents[1] / 'artifacts' / 'group-movement'


def run():
    report = {'checks': []}
    document = None
    canvas = None
    previous = None

    def check(value, description):
        report['checks'].append({'name': description, 'passed': bool(value)})
        assert value, description

    try:
        hosts = list(Rhino.RhinoDoc.OpenDocuments())
        assert hosts and all(d.Path and d.Path.endswith('Margin-QA.3dm') and d.Objects.Count == 0 for d in hosts), 'Use an empty Margin-QA.3dm test session'
        import Grasshopper
        from Grasshopper.Kernel import GH_Document, GH_DocumentIO
        from Grasshopper.Kernel.Parameters import Param_Curve
        from Grasshopper.Kernel.Special import GH_Group
        from Grasshopper.GUI.Canvas import GH_Skin
        from System.Drawing import PointF, Size

        canvas = Grasshopper.Instances.ActiveCanvas
        previous = canvas.Document
        document = GH_Document()
        Grasshopper.Instances.DocumentServer.AddDocument(document)
        canvas.Document = document
        members = []
        for x in (160, 480):
            obj = Param_Curve()
            obj.CreateAttributes()
            obj.Attributes.Pivot = PointF(x, 280)
            document.AddObject(obj, False)
            obj.Attributes.ExpireLayout()
            obj.Attributes.PerformLayout()
            members.append(obj)
        group = GH_Group()
        group.NickName = 'Guide curves'
        for obj in members:
            group.AddObject(obj.InstanceGuid)
        document.AddObject(group, False)
        canvas.Refresh()
        check(group.Attributes.GetType().FullName == 'Margin.Native.StyledGroup', 'native group receives Margin attributes')
        original = group.Attributes.Bounds
        member = members[-1]

        def translate(objects, dx, dy):
            for obj in document.Objects:
                obj.Attributes.Selected = obj in objects
            document.TranslateObjects(Size(dx, dy), True)
            canvas.Refresh()

        for dx, dy in ((160, 180), (-500, -180), (0, 260)):
            translate([member], dx, dy)
            check(group.Attributes.Bounds.Contains(member.Attributes.Bounds), 'frame contains moved member at offset %s,%s' % (dx, dy))
            check(group.ObjectIDs.Contains(member.InstanceGuid), 'movement preserves membership')
            check(group.Attributes.Bounds != original, 'frame resizes from original boundary')
            translate([member], -dx, -dy)
            report.setdefault('restored_frames', []).append({'original':str(original),'restored':str(group.Attributes.Bounds),'member':str(member.Attributes.Bounds)})
            check(group.Attributes.Bounds == original, 'frame shrinks when member returns')

        # An outer group must follow the styled inner frame, including its heading.
        outer = GH_Group()
        outer.NickName = 'Nested group'
        outer.AddObject(group.InstanceGuid)
        document.AddObject(outer, False)
        translate([member], 100, 160)
        check(outer.Attributes.Bounds.Contains(group.Attributes.Bounds), 'nested group contains updated inner frame')
        translate([member], -100, -160)
        document.RemoveObject(outer, False)

        before_positions = [obj.Attributes.Pivot for obj in members]
        # Translate the complete native group selection, as a canvas drag does.
        translate([group] + members, 80, 60)
        check(all(abs(obj.Attributes.Pivot.X - p.X - 80) < .1 and abs(obj.Attributes.Pivot.Y - p.Y - 60) < .1 for obj, p in zip(members, before_positions)), 'group translation moves every member once')
        check(all(group.Attributes.Bounds.Contains(obj.Attributes.Bounds) for obj in members), 'translated frame contains all members')
        translate([group] + members, -80, -60)

        group.RemoveObject(member.InstanceGuid)
        check(group.Attributes.Bounds.Right < original.Right, 'removing member contracts boundary')
        group.AddObject(member.InstanceGuid)
        check(group.Attributes.Bounds.Contains(member.Attributes.Bounds), 'adding member expands boundary')

        translate([member], 100, 180)
        for obj in document.Objects:
            obj.Attributes.Selected = False
        canvas.Viewport.Zoom = 1.1
        canvas.Viewport.MidPoint = PointF(350, 300)
        canvas.Refresh()
        OUTPUT.mkdir(parents=True, exist_ok=True)
        bitmap = canvas.GenerateHiResImageTile(canvas.Viewport, GH_Skin.canvas_back)
        try:
            bitmap.Save(str(OUTPUT / 'moved-member.png'))
        finally:
            bitmap.Dispose()
        io = GH_DocumentIO()
        io.Document = document
        check(io.SaveQuiet(str(OUTPUT / 'Group-movement-QA.gh')), 'movement fixture saves')
        report['passed'] = True
    except Exception:
        report['error'] = traceback.format_exc()
        report['passed'] = False
    finally:
        if canvas is not None:
            canvas.Document = previous
        if document is not None:
            Grasshopper.Instances.DocumentServer.RemoveDocument(document)
            document.Dispose()
        OUTPUT.mkdir(parents=True, exist_ok=True)
        (OUTPUT / 'result.json').write_text(json.dumps(report, indent=2))


Rhino.RhinoApp.InvokeOnUiThread(System.Action(run))
