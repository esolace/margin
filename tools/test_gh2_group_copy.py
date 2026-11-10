"""Run with Rhino 9 Python 3 after enabling a Margin future-group appearance in GH2.

Exercises GH2's normal copy/paste implementation using its app-local clipboard.
All documents are disposable and never assigned to the editor.
"""
import json
from contextlib import ExitStack
from pathlib import Path
from tempfile import TemporaryDirectory

import clr
import System

clr.AddReference("Grasshopper2")
from Eto.Drawing import PointF
from Grasshopper2.Doc import ClipboardKind, Document, DocumentIO, FileContents, IDocumentObject, PasteBehaviour
from Grasshopper2.Parameters import Access
from Grasshopper2.Parameters.Standard import NumberParameter
from Grasshopper2.SpecialObjects import GroupObject
from Grasshopper2.UI.Skinning import Shape


def verify():
    # Both installed adapters expose Margin.Native.Groups. Choose the GH2 assembly explicitly.
    adapter = next(a for a in System.AppDomain.CurrentDomain.GetAssemblies() if a.GetName().Name == "Margin.GH2")
    groups = adapter.GetType("Margin.Native.Groups", True)
    style_type = adapter.GetType("Margin.Native.GroupStyle", True)

    def call(type_, name, *args):
        method = next(m for m in type_.GetMethods() if m.Name == name and len(m.GetParameters()) == len(args))
        return method.Invoke(None, System.Array[System.Object](args))

    def style(fill, opacity, **appearance):
        value = call(style_type, "Parse", json.dumps(dict(Fill=fill, Opacity=opacity, Designed=True, Appearance=appearance)))
        assert value is not None, "Invalid fixture appearance"
        return value

    def edit(doc, group, name, appearance):
        snapshot = call(groups, "Snapshot", doc)
        entry = next(item for item in snapshot.Items if item.Id == str(group.InstanceId))
        edited = call(groups, "BeginEdit", doc, snapshot.Document, entry.Id, entry.Revision).Save(name, appearance, False)
        assert edited.Complete, edited.DefaultsError

    def group_objects(doc, name, *members):
        return doc.Methods.GroupObjects(System.Array[IDocumentObject](members), name, None, None)

    def paste(doc):
        before = {o.InstanceId for o in doc.Objects.Forwards}
        assert doc.Methods.PasteFromClipboard(ClipboardKind.Local, PasteBehaviour.DeselectOldObjects, None), "Native paste failed"
        return [o for o in doc.Objects.Forwards if o.InstanceId not in before]

    def number(doc, name, x, y):
        member = NumberParameter("Number", "Number", "Input", Access.Item)
        member.UserName = name
        doc.Objects.Add(member, PointF(x, y))
        return member

    def open_file(path):
        reader = DocumentIO(None, False, False, False, False)
        assert reader.Open(str(path)), "Could not open " + str(path)
        return reader.Document

    def save_file(doc, path):
        writer = DocumentIO(doc, False, False, False, False)
        assert writer.Save(str(path), FileContents.All), "Could not save " + str(path)

    def group_map(objects):
        # The member names identify the two identically named sibling groups.
        # Resolve by native identity inside THIS paste; any dangling or external ID fails.
        objects = list(objects)
        by_id = {o.InstanceId: o for o in objects}
        def leaves(obj):
            if not isinstance(obj, GroupObject):
                return (obj.UserName,)
            return tuple(sorted(name for id_ in obj.ContentIds for name in leaves(by_id[id_])))
        grouped = [o for o in objects if isinstance(o, GroupObject)]
        mapped = {leaves(g): g for g in grouped}
        assert len(objects) == 5 and len(mapped) == 3, "Expected two members and three distinct groups"
        assert set(mapped) == {("East",), ("West",), ("East", "West")}, "Nested membership changed"
        outer = mapped[("East", "West")]
        assert set(outer.ContentIds) == {mapped[("East",)].InstanceId, mapped[("West",)].InstanceId}
        return mapped

    def check_group(group, name, appearance):
        assert (group.UserName or "") == name, "Native group name changed"
        assert call(groups, "Style", group).Equals(appearance), "Group appearance changed: " + name

    def outline(group):
        for child in group.Content:
            if isinstance(child, GroupObject):
                outline(child)
            else:
                child.Attributes.Layout(Shape.Default)
        group.Attributes.Layout(Shape.Default)
        b = group.Attributes.Bounds
        return (round(b.Width, 2), round(b.Height, 2))

    primary = style("#543264", 71, Text="#FFEBBB", Accent="#DFA021", Border="#171620",
                    Bold=False, TitleSize=23, TitleCase="Uppercase", Heading="Band", Radius=14,
                    BorderWidth=3, ShowAccent=False, ShowDivider=False)
    accent = style("#217658", 43, Text="#DEEAC2", Heading="Plain", TitleCase="Lowercase")
    quiet = style("#2B4059", 59, ShowTitle=False)
    default = groups.GetProperty("Defaults").GetValue(None)
    assert not primary.Equals(default), "The source appearance must differ from the computer-wide default"

    def check_triplet(mapped, east_name="Shared name", east_style=primary):
        check_group(mapped[("East",)], east_name, east_style)
        check_group(mapped[("West",)], "Shared name", accent)
        check_group(mapped[("East", "West")], "", quiet)

    with ExitStack() as stack:
        folder = Path(stack.enter_context(TemporaryDirectory(prefix="margin-gh2-copy-")))
        source = Document.NewInertDocument()
        stack.callback(source.Close)
        destination = Document.NewInertDocument()
        with ExitStack() as dest_scope:
            dest_scope.callback(destination.Close)
            for doc in (source, destination):
                call(groups, "Attach", doc)
            east = group_objects(source, "Shared name", number(source, "East", 280, 100))
            west = group_objects(source, "Shared name", number(source, "West", 70, 210))
            outer = group_objects(source, "", east, west)
            check_group(east, "Shared name", default)
            assert not quiet.Appearance.ShowTitle
            for group, appearance in [(east, primary), (west, accent), (outer, quiet)]:
                edit(source, group, group.UserName or "", appearance)
            originals = group_map(source.Objects.Forwards)
            expected_outlines = {key: outline(g) for key, g in originals.items()}
            original_ids = {o.InstanceId for o in source.Objects.Forwards}
            source.Methods.SelectAll()
            assert source.Methods.CopySelection(ClipboardKind.Local), "Native copy failed"

            local = group_map(paste(source))
            transferred = group_map(paste(destination))
            call(groups, "Attach", source)
            call(groups, "Attach", destination)
            assert original_ids.isdisjoint(o.InstanceId for o in destination.Objects.Forwards)
            for copied in (local, transferred):
                check_triplet(copied)
                assert {key: outline(g) for key, g in copied.items()} == expected_outlines, "Pasted outline dimensions changed"
            destination.Undo.Undo()
            assert destination.Objects.Count == 0, "Undo paste left objects behind"
            destination.Undo.Redo()
            call(groups, "Attach", destination)
            transferred = group_map(destination.Objects.Forwards)
            check_triplet(transferred)

            edit(destination, transferred[("East",)], "Edited copy", quiet)
            check_triplet(originals)
            check_triplet(local)
            destination.Undo.Undo()
            check_triplet(transferred)
            destination.Undo.Redo()
            check_triplet(transferred, "Edited copy", quiet)
            edit(source, east, "Edited original", accent)
            check_triplet(local)
            check_triplet(transferred, "Edited copy", quiet)
            expected_outlines = {key: outline(g) for key, g in transferred.items()}
            copied_file = folder / "Copied groups.ghz"
            save_file(destination, copied_file)

        reopened = open_file(copied_file)
        stack.callback(reopened.Close)
        call(groups, "Attach", reopened)
        restored = group_map(reopened.Objects.Forwards)
        check_triplet(restored, "Edited copy", quiet)
        assert {key: outline(g) for key, g in restored.items()} == expected_outlines, "Reopened outline dimensions changed"

        # Save a stock native definition before Margin encounters it. Opening this group
        # with a configured default must preserve its native colour and unstyled state.
        with ExitStack() as stock_scope:
            stock = Document.NewInertDocument()
            stock_scope.callback(stock.Close)
            existing = group_objects(stock, "Existing group", number(stock, "Stock", 100, 100))
            stock_appearance = call(groups, "Style", existing)
            assert not stock_appearance.Designed
            stock_file = folder / "Existing native group.ghz"
            save_file(stock, stock_file)
        existing_file = open_file(stock_file)
        stack.callback(existing_file.Close)
        call(groups, "Attach", existing_file)
        existing = next(o for o in existing_file.Objects.Forwards if isinstance(o, GroupObject))
        check_group(existing, "Existing group", stock_appearance)
        assert [o.UserName for o in existing.Content] == ["Stock"]

    return dict(adapterMvid=str(adapter.ManifestModule.ModuleVersionId),
                future_default=default.Fill, source_fill=primary.Fill, same_definition=True, cross_definition=True, same_named_and_nested=True,
                blank_and_hidden_title=True, independent_edits=True, native_undo_redo=True,
                saved_closed_reopened=True, existing_native_file=True)


if __name__ == "__main__":
    import Rhino
    def run():
        print(json.dumps(verify(), indent=2))
    Rhino.RhinoApp.InvokeOnUiThread(System.Action(run))
