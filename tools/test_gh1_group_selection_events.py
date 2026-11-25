"""Run through RhinoCode in an empty Margin-QA.3dm session; never on coursework.

Raises canvas mouse events in the test host without moving the physical mouse.
Checks that Margin's observers run after native selection, including title crossing.
"""
import json
import traceback
from pathlib import Path
from urllib.parse import unquote, urlparse
import System
import Rhino
SCRIPT = Path(unquote(urlparse(__file__).path) if __file__.startswith('file:') else __file__)
OUTPUT = SCRIPT.resolve().parents[1] / 'artifacts' / 'group-selection-events'

def run():
    report = {'checks': []}
    try:
        hosts = list(Rhino.RhinoDoc.OpenDocuments())
        assert hosts and all((d.Path and d.Path.endswith('Margin-QA.3dm') and (d.Objects.Count == 0) for d in hosts)), 'Use an empty Margin-QA.3dm test session'
        import clr
        import Grasshopper
        from Grasshopper.Kernel import GH_Document, GH_DocumentIO, GH_ClipboardType
        from Grasshopper.Kernel.Parameters import Param_Curve
        from System.Drawing import PointF, Size
        from System.Windows.Forms import Control, MouseButtons, MouseEventArgs
        from Margin.Native import Groups, GroupStyle
        canvas = Grasshopper.Instances.ActiveCanvas
        previous = canvas.Document
        d = GH_Document()
        checks = report['checks']

        def check(ok, name):
            checks.append({'name': name, 'passed': bool(ok)})
            assert ok, name

        def mouse(name, p):
            q = canvas.Viewport.ProjectPoint(p)
            args = MouseEventArgs(MouseButtons.Left, 1, int(q.X), int(q.Y), 0)
            clr.GetClrType(Control).GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(canvas, System.Array[System.Object]([args]))
        try:
            canvas.Document = d
            canvas.Size = Size(1000, 800)
            canvas.Viewport.Zoom = 1
            canvas.Viewport.MidPoint = PointF(350, 300)
            canvas.Viewport.ComputeProjection()
            members = []
            for x in (200, 450):
                m = Param_Curve()
                m.CreateAttributes()
                m.Attributes.Pivot = PointF(x, 280)
                d.AddObject(m, False)
                m.Attributes.PerformLayout()
                m.Attributes.Selected = True
                members.append(m)
            g = Groups.Create(d)
            check(d.SelectedCount == 3, 'Studio group creation selects frame and contents for immediate copy')
            snap = Groups.Snapshot(d)
            entry = snap.Items[0]
            Groups.BeginEdit(d, snap.Document, entry.Id, entry.Revision).Save('Events', GroupStyle('#6937C4', 96, True))
            d.DeselectAll()
            b = g.Attributes.Bounds
            p = PointF(b.X + 35, b.Y + 20)
            canvas.Viewport.MidPoint = p
            canvas.Viewport.ComputeProjection()
            mouse('OnMouseDown', p)
            check(canvas.ActiveInteraction is not None, 'native interaction exists')
            check(g.Attributes.Selected, 'native group selected')
            mouse('OnMouseUp', p)
            check(d.SelectedCount == 3, 'registered mouse events select frame and contents after native handlers')
            io = GH_DocumentIO(d)
            check(io.Copy(GH_ClipboardType.Local, True) and io.Paste(GH_ClipboardType.Local) and (io.Document.ObjectCount == 3), 'native Copy after real canvas event includes contents')
            io.Document.Dispose()
            d.DeselectAll()
            start = PointF(b.Right + 10, b.Top + 5)
            end = PointF(b.Left - 10, b.Top + 30)
            mouse('OnMouseDown', start)
            mouse('OnMouseMove', end)
            mouse('OnMouseUp', end)
            check(d.SelectedCount == 3, 'registered rectangle events expand title-only crossing selection')
            report['passed'] = True
        finally:
            canvas.ActiveInteraction = None
            canvas.Document = previous
            d.Dispose()
    except Exception:
        report['passed'] = False
        report['error'] = traceback.format_exc()
    finally:
        OUTPUT.mkdir(parents=True, exist_ok=True)
        (OUTPUT / 'result.json').write_text(json.dumps(report, indent=2))
Rhino.RhinoApp.InvokeOnUiThread(System.Action(run))
