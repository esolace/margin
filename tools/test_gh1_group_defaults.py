"""Installed GH1 Studio acceptance, on the Rhino UI thread in Margin-QA.3dm.

Back up Margin preferences first. Run verify(output) and, in a new Rhino process,
verify(output, after_restart=True). Restore preferences after both phases.
Uses disposable definitions and the host's real Group menu command.
"""
import json
import time
from pathlib import Path

import clr
import System
import Rhino
import Grasshopper
from Eto.Forms import Application
from Grasshopper.Kernel import GH_Document
from Grasshopper.Kernel.Parameters import Param_Curve
from Grasshopper.Kernel.Special import GH_Group
from System.Drawing import PointF


def verify(output_directory, after_restart=False):
    assert Path(Rhino.RhinoDoc.ActiveDoc.Path).name == 'Margin-QA.3dm'
    adapter, = [a for a in System.AppDomain.CurrentDomain.GetAssemblies()
                if not a.IsDynamic and a.GetName().Name == 'Margin.Native']
    assert '/packages/' in adapter.Location, 'Test an installed package'
    clr.AddReference(adapter.FullName)
    from Margin.Native import Groups, GroupStyle, Inspector, Runtime
    output = Path(output_directory)
    output.mkdir(parents=True, exist_ok=True)
    report = dict(pid=System.Diagnostics.Process.GetCurrentProcess().Id,
                  mvid=str(adapter.ManifestModule.ModuleVersionId), installed=adapter.Location,
                  rhino=str(Rhino.RhinoApp.Version), runtime=str(System.Environment.Version), checks=[])
    canvas = Grasshopper.Instances.ActiveCanvas
    assert not canvas.IsDisposed
    original = canvas.Document
    document = GH_Document()
    studio = None

    def check(condition, description):
        assert condition, description
        report['checks'].append(description)

    def wait(probe, description):
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            value = probe()
            if value:
                return value
            Application.Instance.RunIteration()
            time.sleep(.02)
        raise AssertionError('Timed out: ' + description)

    def js(script):
        task = studio.InspectAsync(script)
        wait(lambda: task.IsCompleted, 'Studio bridge')
        return task.GetAwaiter().GetResult()

    def open_studio():
        form = Inspector()
        form.Present()
        wait(lambda: form.Ready, 'Studio ready')
        return form

    visible = dict(Fill='#704180', Opacity=72, Designed=True, Appearance=dict(
        Text='#FFDA74', Accent=None, Border=None, TitleSize=22, TitleCase='Uppercase', ShowTitle=True,
        Bold=True, Radius=8, BorderWidth=1, ShowAccent=True, ShowDivider=True, Heading='Rule'))
    hidden = dict(Fill='#246E64', Opacity=48, Designed=True, Appearance=dict(
        Text='#DAFFF1', Accent=None, Border=None, TitleSize=13, TitleCase='Lowercase', ShowTitle=False,
        Bold=False, Radius=3, BorderWidth=2, ShowAccent=False, ShowDivider=False, Heading='Plain'))
    expected = {key: GroupStyle.Parse(json.dumps(value)) for key, value in [('A', visible), ('B', hidden)]}

    def configure(key):
        value = visible if key == 'A' else hidden
        fields = dict(value['Appearance'], **{k: value[k] for k in ('Fill', 'Opacity', 'Designed')})
        js("for(const [k,v] of Object.entries(%s)){const e=document.getElementById('group'+k);"
           "if(v===null){const a=document.getElementById('groupAuto'+k);if(!a.checked)a.click();continue;}if(typeof v==='boolean')e.checked=v;else e.value=v; e.dispatchEvent(new Event('input',{bubbles:true}));}"
           "document.getElementById('saveGroupDefaults').click(); return true;" % json.dumps(fields))
        wait(lambda: Groups.Defaults.Equals(expected[key]), 'saved appearance ' + key)
        report['checks'].append('Studio saved complete appearance ' + key)

    def create(key, via_studio):
        document.DeselectAll()
        member = Param_Curve()
        member.NickName = key + (' Studio' if via_studio else ' Native')
        member.CreateAttributes()
        member.Attributes.Pivot = PointF(160 + document.ObjectCount * 65, 280)
        document.AddObject(member, False)
        member.Attributes.Selected = True
        before = {g.InstanceGuid for g in document.Objects if isinstance(g, GH_Group)}
        if via_studio:
            js("document.getElementById('createGroup').click(); return true;")
        else:
            editor = Grasshopper.Instances.DocumentEditor
            flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            editor.GetType().GetField('_mnuGroupSelection', flags).GetValue(editor).PerformClick()
        group = wait(lambda: next((g for g in document.Objects if isinstance(g, GH_Group)
                                   and g.InstanceGuid not in before), None), 'new group')
        actual = Groups.Style(group)
        report.setdefault('created', []).append(dict(route=member.NickName, name=group.NickName, members=[str(x) for x in group.ObjectIDs], expectedMember=str(member.InstanceGuid), actual=str(actual), expected=str(expected[key])))
        check(not group.NickName and set(group.ObjectIDs) == {member.InstanceGuid}
              and actual.Appearance.Equals(expected[key].Appearance)
              and actual.Fill == expected[key].Fill and abs(actual.Opacity - expected[key].Opacity) < .3,
              member.NickName + ' retained blank name, membership and saved appearance')
        return group

    try:
        Grasshopper.Instances.DocumentServer.AddDocument(document)
        canvas.Document = document
        Groups.Attach(document)
        studio = open_studio()
        js("document.getElementById('groupsTab').click(); return true;")
        if after_restart:
            previous = json.loads((output / 'defaults-before-restart.json').read_text())
            check(previous['passed'] and previous['pid'] != report['pid'] and previous['mvid'] == report['mvid'],
                  'A fresh Rhino process loaded the same installed binary')
            check(Runtime.DesiredEnabled and Runtime.Enabled, 'Labels remained enabled after Rhino restart')
        else:
            configure('A')
            first = create('A', False)
            create('A', True)
            configure('B')
            check(Groups.Style(first).Appearance.Equals(expected['A'].Appearance), 'Saving B preserved existing A group')
            Runtime.SetEnabled(True)
            check(Runtime.Enabled and Runtime.LastError is None, 'Labels enabled without a runtime error')
            document.DeselectAll()
            studio.Close()
            studio = open_studio()
            js("document.getElementById('groupsTab').click(); return true;")
        check(Groups.Defaults.Equals(expected['B']), 'Complete hidden defaults survived reopening')
        wait(lambda: js("return document.getElementById('groupTarget').textContent;") == 'Future groups', 'future default display')
        check(js("return document.getElementById('groupShowTitle').checked ? 'visible':'hidden';") == 'hidden',
              'Studio displayed the saved hidden-title default')
        create('B', False)
        group = create('B', True)
        document.DeselectAll()
        wait(lambda: js("return [...document.getElementById('groupLookup').options].some(o=>o.value==="
                        + json.dumps(str(group.InstanceGuid)) + ") ? 'yes':'';"), 'hidden group in picker')
        js("const e=document.getElementById('groupLookup');e.value=" + json.dumps(str(group.InstanceGuid))
           + ";e.dispatchEvent(new Event('change'));return true;")
        js("document.getElementById('groupShowTitle').click();document.getElementById('applyGroup').click();return true;")
        wait(lambda: Groups.Style(group).Appearance.ShowTitle, 'recovered heading')
        check(not group.NickName and Groups.Defaults.Equals(expected['B']), 'Studio recovered unnamed heading without changing defaults')
        report['passed'] = True
    except Exception:
        import traceback
        report.update(passed=False, error=traceback.format_exc())
        raise
    finally:
        if studio is not None:
            studio.Close()
        canvas.Document = original
        Grasshopper.Instances.DocumentServer.RemoveDocument(document)
        document.Dispose()
        report['originalCanvasPreserved'] = canvas.Document == original
        phase = 'after' if after_restart else 'before'
        (output / ('defaults-' + phase + '-restart.json')).write_text(json.dumps(report, indent=2))
    return report
