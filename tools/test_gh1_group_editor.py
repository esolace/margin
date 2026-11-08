"""RhinoCode: test real Eto group-editor controls on disposable documents.

Never shows a window or replaces the working canvas. Uses the same optional
margin-group-workflow-config.json target as test_gh1_group_workflow.py.
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
OUTPUT = SCRIPT.resolve().parents[1] / 'artifacts' / 'group-editor'


def run():
    report={'checks':[]}; docs=[]; dialogs=[]
    def check(value,name):
        report['checks'].append({'name':name,'passed':bool(value)})
        assert value,name
    try:
        import Grasshopper
        from Grasshopper.Kernel import GH_Document,GH_DocumentIO
        from Grasshopper.Kernel.Special import GH_Group
        from Grasshopper.Kernel.Parameters import Param_Curve
        from Eto.Drawing import Color
        from Eto.Forms import Control
        c=Grasshopper.Instances.ActiveCanvas;current=c.Document
        ids=[str(o.InstanceGuid) for o in current.Objects] if current else []
        config_path=Path(tempfile.gettempdir())/'margin-group-workflow-config.json'
        config=json.loads(config_path.read_text()) if config_path.exists() else {}
        namespace=config.get('namespace','Margin.Native')
        for path in config.get('assemblies',[]):
            assembly=System.Reflection.Assembly.LoadFrom(path);clr.AddReference(assembly.FullName)
        native=importlib.import_module(namespace)
        assembly=clr.GetClrType(native.StyledGroup).Assembly
        factory=assembly.GetType(namespace+'.GroupEditor').GetMethod('CreateDialog',System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
        d=GH_Document();docs.append(d);native.Groups.Attach(d)
        child=Param_Curve();child.CreateAttributes();d.AddObject(child,False)
        group=GH_Group();group.NickName='Guide curves';group.AddObject(child.InstanceGuid);d.AddObject(group,False)
        def edit():
            result=factory.Invoke(None,System.Array[System.Object]([group]));dialogs.append(result);return result
        dialog=edit();picker=dialog.FindChild(clr.GetClrType(Control),'group-title-colour');auto=dialog.FindChild(clr.GetClrType(Control),'group-title-auto');case=dialog.FindChild(clr.GetClrType(Control),'group-title-case')
        check(picker.Enabled and auto.Checked==True,'native picker stays editable while automatic')
        picker.Value=Color.Parse('#FF35A0')
        check(auto.Checked==False,'choosing a native colour switches to custom')
        auto.Checked=True
        dialog.FindChild(clr.GetClrType(Control),'group-fill').Value=Color.Parse('#FFFFFF')
        check(auto.Checked==True and picker.Enabled and picker.Value.ToHex(False)=='#23282C','auto recalculates dark title on light fill without disabling picker')
        dialog.DefaultButton.PerformClick()
        check(native.Groups.Style(group).Appearance.Text is None,'saving Auto removes the custom colour override')
        dialog=edit();picker=dialog.FindChild(clr.GetClrType(Control),'group-title-colour');auto=dialog.FindChild(clr.GetClrType(Control),'group-title-auto');case=dialog.FindChild(clr.GetClrType(Control),'group-title-case')
        picker.Value=Color.Parse('#FF35A0');case.SelectedKey='Uppercase';dialog.DefaultButton.PerformClick()
        style=native.Groups.Style(group)
        check(style.Appearance.Text=='#FF35A0' and style.Appearance.TitleCase=='Uppercase','Save persists colour and letter case')
        check(group.NickName=='Guide curves','casing leaves native group name unchanged')
        d.UndoServer.PerformUndo();check(native.Groups.Style(group).Appearance.Text is None and native.Groups.Style(group).Appearance.TitleCase=='AsTyped','undo restores original appearance')
        d.UndoServer.PerformRedo();check(native.Groups.Style(group).Appearance.TitleCase=='Uppercase','redo restores letter case')
        reopenedDialog=edit()
        check(reopenedDialog.FindChild(clr.GetClrType(Control),'group-title-case').SelectedKey=='Uppercase' and reopenedDialog.FindChild(clr.GetClrType(Control),'group-title-auto').Checked==False,'reopened editor restores casing and custom mode')
        OUTPUT.mkdir(parents=True,exist_ok=True)
        file=str(OUTPUT/'Disposable-title-editor.gh');io=GH_DocumentIO(d);check(io.SaveQuiet(file),'title fixture saves')
        io2=GH_DocumentIO();check(io2.Open(file),'title fixture reopens');docs.append(io2.Document)
        restored=io2.Document.FindObject(group.InstanceGuid,True)
        check(native.Groups.Style(restored).Appearance.Text=='#FF35A0' and native.Groups.Style(restored).Appearance.TitleCase=='Uppercase','reopened definition retains colour and letter case')
        check(System.Object.ReferenceEquals(c.Document,current) and (ids==[str(o.InstanceGuid) for o in current.Objects] if current else True),'working definition stays untouched')
        report['target']=namespace;report['passed']=True
    except Exception:report['passed']=False;report['error']=traceback.format_exc()
    finally:
        for dialog in reversed(dialogs):dialog.Dispose()
        for d in reversed(docs):d.Dispose()
        OUTPUT.mkdir(parents=True,exist_ok=True);(OUTPUT/'result.json').write_text(json.dumps(report,indent=2))
Rhino.RhinoApp.InvokeOnUiThread(System.Action(run))
