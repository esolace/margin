"""Installed Mac GH2 Studio acceptance. Run on the Rhino UI thread in Margin-QA.3dm.

Call verify(output_directory) with backed-up preferences in a disposable Rhino
process. This intentionally leaves style B saved so a new process can run
verify(output_directory, after_restart=True). Restore the original preferences
after both runs. Both phases create and close their own GH2 definitions.
"""
import json
import time
from pathlib import Path

import Rhino
import System
from Eto.Drawing import ImageFormat, PointF, RectangleF
from Eto.Forms import Application
from Grasshopper2.Doc import ClipboardKind, Document, DocumentIO, FileContents, ObjectSelection, PasteBehaviour
from Grasshopper2.Parameters.Standard import NumberParameter
from Grasshopper2.SpecialObjects import GroupObject
from Grasshopper2.UI import Editor


STYLE_A = dict(Fill='#704180', Opacity=72, Designed=True, Appearance=dict(
    Text='#FFDA74', Accent='#D999FF', Border='#382044', TitleSize=22, Bold=True,
    Radius=12, BorderWidth=3, ShowTitle=True, ShowAccent=True, ShowDivider=False,
    TitleCase='Uppercase', Heading='Band'))
STYLE_B = dict(Fill='#246E64', Opacity=48, Designed=True, Appearance=dict(
    Text='#DAFFF1', Accent='#96ECD9', Border='#173C37', TitleSize=13, Bold=False,
    Radius=3, BorderWidth=2, ShowTitle=False, ShowAccent=False, ShowDivider=True,
    TitleCase='Lowercase', Heading='Rule'))


def wait_for(probe, description):
    deadline = time.monotonic() + 6
    while time.monotonic() < deadline:
        value = probe()
        if value:
            return value
        Application.Instance.RunIteration()
        time.sleep(.02)
    raise AssertionError('Timed out waiting for ' + description)


def verify(output_directory, after_restart=False):
    assert Path(Rhino.RhinoDoc.ActiveDoc.Path).name == 'Margin-QA.3dm', 'Use the disposable Rhino QA process'
    output = Path(output_directory)
    output.mkdir(parents=True, exist_ok=True)
    assemblies = System.AppDomain.CurrentDomain.GetAssemblies()
    adapter, = [a for a in assemblies if a.GetName().Name == 'Margin.GH2']
    host, = [a for a in assemblies if a.GetName().Name == 'Grasshopper2']
    assert '/packages/9.0/' in adapter.Location, 'Test the installed package'
    groups = adapter.GetType('Margin.Native.Groups', True)
    styles = adapter.GetType('Margin.Native.GroupStyle', True)
    studio_type = adapter.GetType('Margin.Native.Inspector', True)
    args = System.Array[System.Object]
    expected = {key: styles.GetMethod('Parse').Invoke(None, args([json.dumps(data)]))
                for key, data in [('A', STYLE_A), ('B', STYLE_B)]}
    def defaults():
        return groups.GetProperty('Defaults').GetValue(None)
    def read(group):
        return groups.GetMethod('Style').Invoke(None, args([group]))

    canvas = Editor.Instance.Canvas
    original = canvas.Document
    original_ids = {o.InstanceId for o in original.Objects.Forwards}
    source = Document.NewInertDocument()
    destination = Document.NewInertDocument()
    studio = None
    checks = []
    pid = System.Diagnostics.Process.GetCurrentProcess().Id
    mvid = str(adapter.ManifestModule.ModuleVersionId)
    report = dict(pid=pid, rhino=str(Rhino.RhinoApp.Version), gh2=str(host.FullName),
                  gh2File=System.Diagnostics.FileVersionInfo.GetVersionInfo(host.Location).FileVersion,
                  margin=str(adapter.FullName), installed=adapter.Location, mvid=mvid, checks=checks)
    def check(condition, description):
        assert condition, description
        checks.append(description)
    def js(script):
        task = studio.InspectAsync(script)
        wait_for(lambda: task.IsCompleted, 'Studio WebKit reply')
        return task.GetAwaiter().GetResult()
    def open_studio():
        form = System.Activator.CreateInstance(studio_type)
        form.Present()
        return form
    def capture(name):
        bitmap = canvas.DrawToBitmap(RectangleF(0, -65, 560, 310), 1008, 558, True, True, False)
        try:
            bitmap.Save(str(output / (name + '.png')), ImageFormat.Png)
        finally:
            bitmap.Dispose()
    def configure(key):
        style = STYLE_A if key == 'A' else STYLE_B
        fields = dict(style['Appearance'], **{k: style[k] for k in ('Fill', 'Opacity', 'Designed')})
        js("""
            for (const [name,value] of Object.entries(%s)) {
              const el = document.getElementById('group'+name);
              if (typeof value==='boolean') el.checked=value; else el.value=value;
              el.dispatchEvent(new Event('input',{bubbles:true}));
            }
            document.getElementById('saveGroupDefaults').click();
            return true;
        """ % json.dumps(fields))
        wait_for(lambda: defaults().Equals(expected[key]), 'Studio to save style ' + key)
        checks.append('Studio saved complete appearance ' + key)
    def create(key, via_studio):
        member = NumberParameter()
        member.UserName = key + (' Studio' if via_studio else ' Native')
        source.Objects.Add(member, PointF(100 + source.Objects.Count * 60, 150 if via_studio else 60))
        source.Methods.DeselectAll()
        member.Selection = ObjectSelection.Selected
        before = {o.InstanceId for o in source.Objects.Groups}
        if via_studio:
            js("document.getElementById('createGroup').click(); return true;")
            group = wait_for(lambda: next((g for g in source.Objects.Groups if g.InstanceId not in before), None),
                             'Studio to create a native group')
        else:
            group = source.Methods.GroupSelection(None, None, None)
        check(isinstance(group, GroupObject) and set(group.ContentIds) == {member.InstanceId}
              and not group.UserName and read(group).Equals(expected[key]),
              member.UserName + ' kept blank name, exact member identity and complete saved appearance')
        return group, member

    try:
        canvas.Document = source
        studio = open_studio()
        wait_for(lambda: studio.Ready, 'native Studio to connect')
        js("document.getElementById('groupsTab').click(); return true;")
        if after_restart:
            previous = json.loads((output / 'defaults-before-restart.json').read_text())
            check(previous['passed'] and previous['pid'] != pid and previous['mvid'] == mvid,
                  'Fresh Rhino process loaded the same installed candidate')
        else:
            configure('A')
            original_group, original_member = create('A', False)
            create('A', True)
            capture('both-routes-visible-A')
            source.Methods.DeselectAll()
            original_group.Selection = ObjectSelection.Selected
            wait_for(lambda: js("return window.MarginStudio.inspect().groupId || '';") == str(original_group.InstanceId),
                     'Studio to identify the selected unnamed group')
            check(js("return document.getElementById('groupTarget').textContent;") == 'Group',
                  'Studio identified the selected unnamed group as Group')
            configure('B')
            check(read(original_group).Equals(expected['A']), 'Saving B left the selected A group untouched')
            source.Methods.DeselectAll()
            studio.Close()
            studio = open_studio()
            wait_for(lambda: studio.Ready, 'reopened Studio to connect')
            js("document.getElementById('groupsTab').click(); return true;")
        check(defaults().Equals(expected['B']), 'Complete hidden-title B defaults survived reopening')
        wait_for(lambda: js("return document.getElementById('groupTarget').textContent;") == 'Future groups',
                 'Studio to edit future groups')
        check(js("return document.getElementById('groupShowTitle').checked ? 'visible' : 'hidden';") == 'hidden'
              and js("return document.getElementById('groupFill').value;").upper() == STYLE_B['Fill'],
              'Reopened Studio displayed the hidden B default')
        create('B', False)
        hidden_group, hidden_member = create('B', True)
        capture('both-routes-hidden-B' + ('-restarted' if after_restart else ''))
        source.Methods.DeselectAll()
        # Detectable through the definition picker even with a blank name and no canvas title.
        wait_for(lambda: js("return [...document.getElementById('groupLookup').options].some(o => o.value === "
                            + json.dumps(str(hidden_group.InstanceId)) + ") ? 'yes' : '';"), 'hidden unnamed group in Studio')
        js("const picker=document.getElementById('groupLookup'); picker.value=" + json.dumps(str(hidden_group.InstanceId))
           + "; picker.dispatchEvent(new Event('change')); return true;")
        check(js("return document.getElementById('groupTarget').textContent;") == 'Group'
              and js("return document.getElementById('groupShowTitle').checked ? 'visible' : 'hidden';") == 'hidden',
              'Studio located the hidden unnamed group with its title control off')
        js("document.getElementById('groupShowTitle').click(); document.getElementById('applyGroup').click(); return true;")
        wait_for(lambda: read(hidden_group).Appearance.ShowTitle, 'Studio to restore the title')
        check(not hidden_group.UserName and set(hidden_group.ContentIds) == {hidden_member.InstanceId}
              and defaults().Equals(expected['B']), 'Studio restored the heading without renaming, regrouping or changing defaults')
        wait_for(lambda: js("return window.MarginStudio.inspect().groupPending ? '' : 'ready';"),
                 'Studio to acknowledge Apply')
        source.Undo.Undo()
        check(read(hidden_group).Equals(expected['B']), 'One undo restored the complete hidden appearance')
        # Wait for Studio's selection timer to observe undo before invoking another command.
        wait_for(lambda: js("return window.MarginStudio.inspect().groups.Items.find(g=>g.Id === "
                            + json.dumps(str(hidden_group.InstanceId))
                            + ")?.Style.Appearance.ShowTitle === false ? 'yes' : '';"), 'Studio to observe undo')
        source.Methods.DeselectAll()
        count = source.Objects.Count
        check(source.Methods.GroupSelection(None, None, None) is None and source.Objects.Count == count,
              'Native empty selection left the definition intact')
        js("document.getElementById('createGroup').click(); return true;")
        wait_for(lambda: 'Select components' in js("return document.getElementById('groupStatus').textContent;"),
                 'actionable empty-selection feedback')
        check(source.Objects.Count == count, 'Empty Studio selection showed feedback and left the definition intact')
        if not after_restart:
            original_group.Selection = original_member.Selection = ObjectSelection.Selected
            assert source.Methods.CopySelection(ClipboardKind.Local)
            for doc, label in [(source, 'same definition'), (destination, 'second definition')]:
                canvas.Document = doc
                before = {o.InstanceId for o in doc.Objects.Forwards}
                assert doc.Methods.PasteFromClipboard(ClipboardKind.Local, PasteBehaviour.DeselectOldObjects, None)
                pasted = [o for o in doc.Objects.Forwards if o.InstanceId not in before]
                group, = [o for o in pasted if isinstance(o, GroupObject)]
                member, = [o for o in pasted if isinstance(o, NumberParameter)]
                check(not group.UserName and read(group).Equals(expected['A']) and
                      set(group.ContentIds) == {member.InstanceId} and member.UserName == original_member.UserName
                      and member.InstanceId != original_member.InstanceId,
                      'A pasted into ' + label + ' under B retained its appearance and remapped member')
            saved_path = str(output / 'style-A-under-B.ghz')
            assert DocumentIO(destination, False, False, False, False).Save(saved_path, FileContents.All)
            canvas.Document = source
            destination.Close()
            destination = None
            reader = DocumentIO(None, False, False, False, False)
            assert reader.Open(saved_path)
            destination = reader.Document
            canvas.Document = destination
            reopened, = list(destination.Objects.Groups)
            check(read(reopened).Equals(expected['A']) and not reopened.UserName
                  and [m.UserName for m in reopened.Content] == [original_member.UserName]
                  and defaults().Equals(expected['B']), 'Closing and reopening the destination kept A under B')
        report['passed'] = True
    except Exception:
        import traceback
        report['passed'] = False
        report['error'] = traceback.format_exc()
        raise
    finally:
        if studio is not None:
            studio.Close()
        canvas.Document = original
        if destination is not None:
            destination.Close()
        source.Close()
        report['originalCanvasPreserved'] = (canvas.Document.Identity == original.Identity and
                                             original_ids == {o.InstanceId for o in original.Objects.Forwards})
        report['passed'] = report.get('passed', False) and report['originalCanvasPreserved']
        phase = 'after' if after_restart else 'before'
        (output / ('defaults-' + phase + '-restart.json')).write_text(json.dumps(report, indent=2))
    assert report['passed']
    return report
