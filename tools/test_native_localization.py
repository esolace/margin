"""Rhino UI-thread localization checks using isolated settings and disposable documents.

Call verify(output) only in a disposable, activation-prohibited Rhino process
with MARGIN_SETTINGS_DIRECTORY pointing at its private settings folder.
verify does not show UI, change the clipboard, or assign the active canvas document.
verify_studio also supports Margin.GH2 and optionally presents Studio when visible=True.
"""
from pathlib import Path
import json
import traceback


def cleanup(objects, report):
    for name, obj, close in objects:
        if close:
            try:
                obj.Close()
            except BaseException:
                report.setdefault('cleanupFailures', []).append(name + ' Close: ' + traceback.format_exc())
        try:
            if hasattr(obj, 'Dispose'): obj.Dispose()
            else: obj.Close()
        except BaseException:
            report.setdefault('cleanupFailures', []).append(name + ' Dispose: ' + traceback.format_exc())


def verify(output):
    import clr, System, Rhino
    from Eto.Forms import Application, Control
    from Margin.Core import Localization, PreferencesStore
    from Margin.Native import Runtime, Groups, GroupStyle
    from Grasshopper.Kernel import GH_Document, GH_DocumentIO
    from Grasshopper.Kernel.Parameters import Param_Curve
    from Grasshopper.Kernel.Special import GH_Group, GH_GroupBorder
    from System.Drawing import PointF
    import Grasshopper
    output = Path(output).resolve()
    private = System.Environment.GetEnvironmentVariable('MARGIN_SETTINGS_DIRECTORY')
    assert private and Path(Runtime.PreferencesPath).parent.resolve() == Path(private).resolve()
    assert output in Path(private).resolve().parents, 'Settings must be inside the disposable output'
    app = Application.Instance.ControlObject
    assert str(app.ActivationPolicy) == 'Prohibited' and not app.Active
    report = {'rhino': str(Rhino.RhinoApp.Version), 'pid': System.Diagnostics.Process.GetCurrentProcess().Id, 'checks': []}
    def check(condition, name):
        report['checks'].append({'name': name, 'passed': bool(condition)})
        assert condition, name
    flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
    assembly = clr.GetClrType(Runtime).Assembly
    factory = assembly.GetType('Margin.Native.GroupEditor').GetMethod('CreateDialog', flags)
    save_language = assembly.GetType('Margin.Native.NativeLanguage').GetMethod('Save', flags)
    canvas = Grasshopper.Instances.ActiveCanvas
    before = canvas.Document if canvas else None
    docs, dialogs = [], []
    try:
        for language in Localization.Languages.Keys:
            save_language.Invoke(None, System.Array[System.Object]([language]))
            check(PreferencesStore.Load(Runtime.PreferencesPath).Language == language, language + ': language persists in private settings')
            d = GH_Document(); docs.append(d); Groups.Attach(d)
            child = Param_Curve(); child.CreateAttributes(); child.Attributes.Pivot = PointF(200, 250)
            d.AddObject(child, False); child.Attributes.Selected = True
            group = Groups.Create(d)
            check(group.NickName == '' and Groups.Style(group).Designed, language + ': normal new group keeps an empty stored name')
            dialog = factory.Invoke(None, System.Array[System.Object]([group])); dialogs.append(dialog)
            def field(key): return dialog.FindChild(clr.GetClrType(Control), key)
            check(dialog.Title == Localization.Text('group.edit') and dialog.DefaultButton.Text == Localization.Text('ui.save')
                  and dialog.AbortButton.Text == Localization.Text('ui.cancel')
                  and field('group-show-title').Text == Localization.Text('ui.show_title'), language + ': native editor copy matches catalog')
            check(all(item.Text == Localization.Text('case.' + item.Key) for item in field('group-title-case').Items), language + ': native casing choices translated without changing keys')
            name = '日本語 · Český · Русский · <b>{name}</b>'
            field('group-name').Text = name
            field('group-title-case').SelectedKey = 'Uppercase'
            field('group-show-title').Checked = False
            dialog.DefaultButton.PerformClick()
            style = Groups.Style(group)
            check(group.NickName == name and not style.Appearance.ShowTitle and style.Appearance.TitleCase == 'Uppercase', language + ': native save preserves literal multilingual names and hidden-title choice')
            d.UndoServer.PerformUndo()
            check(group.NickName == '' and Groups.Style(group).Appearance.ShowTitle, language + ': native undo restores blank name and visible title')
            d.UndoServer.PerformRedo()
            check(group.NickName == name and Groups.Style(group).Equals(style), language + ': native redo restores complete group state')
            for border in System.Enum.GetValues(clr.GetClrType(GH_GroupBorder)):
                group.Border = border
                duplicate = GH_Document.DuplicateDocument(d); docs.append(duplicate); duplicate.MutateAllIds(); Groups.Attach(duplicate)
                copy = next(o for o in duplicate.Objects if isinstance(o, GH_Group))
                check(copy.NickName == name and copy.Border == border and Groups.Style(copy).Equals(style), language + ': copy preserves name, style and ' + str(border) + ' outline')
            path = str(output / ('group-' + language + '.gh'))
            io = GH_DocumentIO(d); check(io.SaveQuiet(path), language + ': definition saves')
            opened = GH_DocumentIO(); check(opened.Open(path), language + ': definition reopens'); docs.append(opened.Document)
            restored = opened.Document.FindObject(group.InstanceGuid, True)
            check(restored.NickName == name and Groups.Style(restored).Equals(style), language + ': reopen preserves multilingual name and complete style')
            dialog = factory.Invoke(None, System.Array[System.Object]([restored])); dialogs.append(dialog)
            dialog.AbortButton.PerformClick()
            check(restored.NickName == name and Groups.Style(restored).Equals(style), language + ': translated Cancel leaves the reopened group intact')
        save_language.Invoke(None, System.Array[System.Object](['auto']))
        native_host = assembly.GetType('Margin.Native.NativeLanguage').GetProperty('HostLanguage', flags).GetValue(None)
        check(Localization.Language == Localization.Resolve('auto', native_host), 'automatic language follows the actual Rhino locale')
        check(System.Object.ReferenceEquals(before, canvas.Document if canvas else None), 'existing active canvas document unchanged')
        check(not app.Active and str(app.ActivationPolicy) == 'Prohibited', 'native checks finish without activation')
    except BaseException:
        report['failure'] = traceback.format_exc()
    finally:
        try:
            cleanup([('dialog', item, False) for item in reversed(dialogs)] + [('document', item, False) for item in reversed(docs)], report)
        finally:
            output.mkdir(parents=True, exist_ok=True)
            (output / 'native-editor.json').write_text(json.dumps(report, indent=2, ensure_ascii=False))
    return {'passed': sum(item['passed'] for item in report['checks']), 'failure': report.get('failure'), 'cleanupFailures': report.get('cleanupFailures', [])}


def verify_studio(output, adapter_name='Margin.Native', visible=False):
    import clr, System, Rhino, time
    from Eto.Forms import Application
    from Eto.Drawing import Size
    from Margin.Core import Localization, PreferencesStore
    adapter = next(a for a in System.AppDomain.CurrentDomain.GetAssemblies() if a.GetName().Name == adapter_name)
    gh2 = adapter_name == 'Margin.GH2'
    def call(type_, name, *args):
        method = next(m for m in type_.GetMethods() if m.Name == name and len(m.GetParameters()) == len(args))
        return method.Invoke(None, System.Array[System.Object](args))
    runtime = adapter.GetType('Margin.GH2.Runtime' if gh2 else 'Margin.Native.Runtime')
    groups = adapter.GetType('Margin.Native.Groups')
    class Runtime:
        PreferencesPath = runtime.GetProperty('PreferencesPath').GetValue(None)
        Start = staticmethod(lambda: call(runtime, 'Start'))
        Stop = staticmethod(lambda: call(runtime, 'Stop'))
    class GroupBridge:
        @property
        def Defaults(self): return groups.GetProperty('Defaults').GetValue(None)
        def Attach(self, doc): return call(groups, 'Attach', doc)
        def Create(self, doc): return call(groups, 'Create', doc)
        def Style(self, group): return call(groups, 'Style', group)
    Groups = GroupBridge()
    def Inspector(): return System.Activator.CreateInstance(adapter.GetType('Margin.Native.Inspector'))
    output = Path(output).resolve()
    private = System.Environment.GetEnvironmentVariable('MARGIN_SETTINGS_DIRECTORY')
    assert private and Path(Runtime.PreferencesPath).parent.resolve() == Path(private).resolve()
    assert output in Path(private).resolve().parents
    assert not (Path(private) / 'presets.json').exists(), 'Use a fresh private preset library for the Studio test'
    app = Application.Instance.ControlObject
    if not visible: assert str(app.ActivationPolicy) == 'Prohibited' and not app.Active
    report = {'rhino': str(Rhino.RhinoApp.Version), 'adapter': adapter_name, 'visible': visible, 'checks': []}; forms = []; docs = []
    def check(condition, name):
        report['checks'].append({'name': name, 'passed': bool(condition)})
        assert condition, name
    def wait(probe, name):
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            if probe(): return
            Application.Instance.RunIteration(); time.sleep(.01)
        raise AssertionError('Timed out: ' + name)
    def open_studio():
        form = Inspector(); forms.append(form); form.ClientSize = Size(900, 600)
        if visible: form.Present()
        wait(lambda: form.Ready, 'native Studio loads')
        return form
    def js(source):
        task = studio.InspectAsync(source)
        wait(lambda: task.IsCompleted, 'native WebView JavaScript')
        return task.GetAwaiter().GetResult()
    def state(): return json.loads(js('return JSON.stringify(MarginStudio.inspect())'))
    try:
        studio = open_studio()
        check(studio.FontCount > 0 and studio.Visible == visible, 'native Studio loads installed fonts with the requested visibility')
        for language in Localization.Languages.Keys:
            js("const e=document.getElementById('languageChoice');e.value=%s;e.dispatchEvent(new Event('change'));return true;" % json.dumps(language))
            wait(lambda: Localization.Choice == language, language + ': language reaches native adapter')
            wait(lambda: state()['languageChoice'] == language and not json.loads(js("return JSON.stringify(document.getElementById('languageChoice').disabled)")), language + ': acknowledgement')
            check(PreferencesStore.Load(Runtime.PreferencesPath).Language == language, language + ': real Studio bridge saves language')
            check(js("return document.getElementById('groupsTab').textContent") == Localization.Text('ui.groups'), language + ': native WebView displays translated controls')
            name = language + ' · 日本語 · <b>{name}</b>'
            js("document.getElementById('labelsTab').click();document.getElementById('newPreset').click();document.getElementById('presetName').value=%s;document.getElementById('presetSubmit').click();return true;" % json.dumps(name))
            wait(lambda: any(p['Theme']['Name'] == name for p in state()['userPresets']), language + ': native preset save')
            check(js("return document.getElementById('status').textContent") == Localization.Text('ui.preset_saved_on_this_computer'), language + ': native action feedback translated')
            check(not json.loads(js("return JSON.stringify({open:document.getElementById('presetDialog').open})"))['open'], language + ': saved preset dialog closes')
        js("document.getElementById('groupsTab').click();return true;")
        js("for(const [id,value] of Object.entries({groupFill:'#246E64',groupOpacity:48,groupShowTitle:false,groupTitleCase:'Uppercase'})){const e=document.getElementById(id);if(typeof value==='boolean')e.checked=value;else e.value=value;e.dispatchEvent(new Event('input',{bubbles:true}));}document.getElementById('saveGroupDefaults').click();return true;")
        wait(lambda: Groups.Defaults.Fill == '#246E64' and not Groups.Defaults.Appearance.ShowTitle, 'translated Studio saves future group appearance')
        if gh2:
            from Grasshopper2.Doc import Document, ObjectSelection
            from Grasshopper2.Parameters.Standard import NumberParameter
            from Eto.Drawing import PointF
            d = Document.NewInertDocument(); docs.append(d); Groups.Attach(d)
            child = NumberParameter(); d.Objects.Add(child, PointF(100, 100)); child.Selection = ObjectSelection.Selected
        else:
            from Grasshopper.Kernel import GH_Document
            from Grasshopper.Kernel.Parameters import Param_Curve
            d = GH_Document(); docs.append(d); Groups.Attach(d)
            child = Param_Curve(); child.CreateAttributes(); d.AddObject(child, False); child.Attributes.Selected = True
        group = Groups.Create(d)
        actual, expected = Groups.Style(group), Groups.Defaults
        # GH1 stores opacity in an 8-bit native colour (48% becomes 47.8%).
        check(actual.Fill == expected.Fill and actual.Designed == expected.Designed
              and actual.Appearance.Equals(expected.Appearance)
              and (actual.Opacity == expected.Opacity if gh2 else abs(actual.Opacity - expected.Opacity) < .3)
              and not (group.UserName if gh2 else group.NickName), 'new native group uses appearance saved in translated Studio and keeps blank name')
        before = Path(Runtime.PreferencesPath).read_bytes()
        js("window.eto.postMessage(JSON.stringify({action:'language',choice:'not-a-language'}));return true;")
        wait(lambda: js("return document.getElementById('status').textContent") == Localization.Text('change.failed').replace('{error}', Localization.Text('language.invalid')), 'translated native validation detail')
        check(Path(Runtime.PreferencesPath).read_bytes() == before, 'invalid bridge language preserves the saved preference file')
        check(state()['languageChoice'] == 'zh-Hant', 'failed native save restores the acknowledged language')
        studio.Close(); studio.Dispose(); forms.remove(studio)
        Runtime.Stop(); Runtime.Start()
        studio = open_studio()
        reopened = state()
        check(reopened['languageChoice'] == 'zh-Hant' and len(reopened['userPresets']) == 13, 'runtime reinitialization and Studio reopen retain language and all multilingual preset names')
        check(Groups.Defaults.Fill == '#246E64' and not Groups.Defaults.Appearance.ShowTitle, 'runtime reinitialization retains complete future group appearance')
        if not visible: check(not app.Active and str(app.ActivationPolicy) == 'Prohibited', 'Studio checks complete without activation')
        check(studio.LastError is None, 'native Studio reports no internal error')
    except BaseException:
        report['failure'] = traceback.format_exc()
    finally:
        try:
            cleanup([('Studio', item, True) for item in reversed(forms)] + [('document', item, False) for item in reversed(docs)], report)
        finally:
            (output / ('native-studio-gh2.json' if gh2 else 'native-studio.json')).write_text(json.dumps(report, indent=2, ensure_ascii=False))
    return {'passed': sum(item['passed'] for item in report['checks']), 'failure': report.get('failure'), 'cleanupFailures': report.get('cleanupFailures', [])}
