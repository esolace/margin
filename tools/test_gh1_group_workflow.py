"""RhinoCode regression: local copy/paste, pick, rename, move, ungroup, undo/reopen.

Uses disposable documents, never assigns Instances.ActiveCanvas.Document and
never touches the system clipboard. Optional temp/margin-group-workflow-config.json
selects a separately named QA assembly: {"namespace": "MarginQa.Native",
"assemblies": ["/absolute/Core.dll", "/absolute/Native.dll"], "run": "qa"}.
"""
import importlib
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
        Groups.BeginEdit(d, snapshot.Document, entry.Id, entry.Revision).Save('Guide curves', GroupStyle('#6937C4', 96, True))
        # DuplicateDocument uses native GH_DocumentIO.Copy/Paste with its LOCAL clipboard.
        duplicate = GH_Document.DuplicateDocument(d); docs.append(duplicate)
        duplicate.MutateAllIds(); d.MergeDocument(duplicate)
        pasted = next(o for o in d.Objects if isinstance(o, GH_Group) and o.InstanceGuid != group.InstanceGuid)
        pasted_id = pasted.InstanceGuid
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
        check(saved.NickName == 'Independent copy' and saved.ObjectIDs.Contains(child.InstanceGuid), 'pasted rename and membership survive reopen')
        b = saved.Attributes.Bounds
        check(System.Object.ReferenceEquals(reopened.Document.FindAttribute(PointF(b.X + 35, b.Y + 20), True), saved.Attributes),
              'reopened pasted group remains clickable')
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
