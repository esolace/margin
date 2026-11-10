"""Native GH2 acceptance helper. Call on Rhino's UI thread with a disposable
document containing a visible, Margin-styled group. It creates real Eto dialogs
without showing a modal window or changing preferences. The caller owns the fixture.
"""
def verify_title_editor(assembly, document, group, check):
    import clr
    import System
    from Eto.Forms import Control

    flags = System.Reflection.BindingFlags
    factory = assembly.GetType('Margin.Native.GroupEditor', True).GetMethod('CreateDialog', flags.Static | flags.NonPublic)
    read_style = assembly.GetType('Margin.Native.Groups', True).GetMethod('Style')
    args = System.Array[System.Object]([group])
    dialogs = []
    def edit():
        dialog = factory.Invoke(None, args)
        dialogs.append(dialog)
        return dialog
    def field(dialog, key):
        return dialog.FindChild(clr.GetClrType(Control), key)
    def style():
        return read_style.Invoke(None, args)
    def members():
        return tuple(str(id) for id in group.ContentIds)

    assert System.Object.ReferenceEquals(document, group.Document)
    original_style = style()
    assert original_style.Designed and original_style.Appearance.ShowTitle, 'Start with a visible styled title'
    original_name = group.UserName or ''
    original_members = members()
    try:
        dialog = edit()
        field(dialog, 'group-name').Text = 'Cancelled rename'
        field(dialog, 'group-show-title').Checked = False
        dialog.AbortButton.PerformClick()
        check((group.UserName or '') == original_name and style().Equals(original_style),
              'GH2 editor Cancel preserves name and complete appearance')

        dialog = edit()
        field(dialog, 'group-name').Text = ''
        field(dialog, 'group-show-title').Checked = False
        dialog.DefaultButton.PerformClick()
        hidden = style()
        check(not group.UserName and not hidden.Appearance.ShowTitle and hidden.Designed
              and hidden.Fill == original_style.Fill and hidden.Opacity == original_style.Opacity and members() == original_members,
              'GH2 editor saves a blank native name and hides the heading without changing the styled frame or members')

        document.Undo.Undo()
        check((group.UserName or '') == original_name and style().Equals(original_style),
              'one GH2 undo restores the original name and complete appearance')
        document.Undo.Redo()
        check(not group.UserName and style().Equals(hidden), 'GH2 redo restores hidden unnamed state')

        dialog = edit()
        check(not field(dialog, 'group-show-title').Checked and not field(dialog, 'group-name').Text,
              'GH2 editor reopens with the original blank name and persisted visibility')
        field(dialog, 'group-name').Text = 'gUiDe curves'
        field(dialog, 'group-title-case').SelectedKey = 'Uppercase'
        field(dialog, 'group-show-title').Checked = True
        dialog.DefaultButton.PerformClick()
        check(group.UserName == 'gUiDe curves' and style().Appearance.TitleCase == 'Uppercase'
              and style().Appearance.ShowTitle, 'GH2 restores the title and keeps display casing separate from its name')
        document.Undo.Undo()
        document.Undo.Undo()
        check((group.UserName or '') == original_name and style().Equals(original_style) and members() == original_members,
              'GH2 editor fixture restores its original group')
    finally:
        for dialog in reversed(dialogs):
            dialog.Dispose()
