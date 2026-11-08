using Eto.Forms;
using Eto.Drawing;
using Margin.Core;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel.Special;

namespace Margin.Native;

// Edits the native group, with the same undo/revision checks as Studio.
internal static class GroupEditor
{
    public static void Show(GH_Group group)
    {
        using var dialog = CreateDialog(group);
        dialog?.ShowModal(Runtime.Owner);
    }
    internal static Dialog<bool>? CreateDialog(GH_Group group)
    {
        var doc = group.OnPingDocument();
        if (doc is null) return null;
        var snapshot = Groups.Snapshot(doc);
        var entry = snapshot.Items.First(g => g.Id == group.InstanceGuid.ToString());
        var edit = Groups.BeginEdit(doc, snapshot.Document!, entry.Id, entry.Revision);
        var dialog = new Dialog<bool> { Title = "Edit group", Padding = new Padding(20), Resizable = false };
        var name = new TextBox { Text = entry.Name, PlaceholderText = "Group name", Width = 320 };
        var titleCase = new DropDown { ID = "group-title-case", Width = 180 };
        foreach (var option in GroupAppearancePolicy.TitleCases) titleCase.Items.Add(new ListItem { Key = option.Key, Text = option.Value });
        titleCase.SelectedKey = entry.Style.Appearance.TitleCase;
        var colour = new ColorPicker { ID = "group-fill", Value = Color.Parse(entry.Style.Fill) };
        var autoText = new CheckBox { ID = "group-title-auto", Text = "Automatic title contrast", Checked = entry.Style.Appearance.Text is null };
        var canvasColour = GH_Skin.canvas_back;
        var canvasHex = $"#{canvasColour.R:X2}{canvasColour.G:X2}{canvasColour.B:X2}";
        var resolved = GroupAppearancePolicy.Resolve(entry.Style.Appearance, entry.Style.Fill, entry.Style.Opacity, canvasHex);
        var textColour = new ColorPicker { ID = "group-title-colour", Value = Color.Parse(resolved.Text) };
        var updatingColour = false;
        textColour.ValueChanged += (_, _) => { if (!updatingColour) autoText.Checked = false; };
        var opacity = new NumericStepper { MinValue = 0, MaxValue = 100, Value = entry.Style.Opacity };
        void UpdateAutomaticColour()
        {
            if (autoText.Checked != true) return;
            var appearance = entry.Style.Appearance with { Text = null };
            var next = GroupAppearancePolicy.Resolve(appearance, colour.Value.ToHex(false), opacity.Value, canvasHex);
            updatingColour = true;
            try { textColour.Value = Color.Parse(next.Text); }
            finally { updatingColour = false; }
        }
        autoText.CheckedChanged += (_, _) => UpdateAutomaticColour();
        colour.ValueChanged += (_, _) => UpdateAutomaticColour();
        opacity.ValueChanged += (_, _) => UpdateAutomaticColour();
        var designed = new CheckBox { Text = "Show Margin title", Checked = entry.Style.Designed };
        var defaults = new CheckBox { Text = "Use this appearance for future groups" };
        var error = new Label { TextColor = Colors.IndianRed, Wrap = WrapMode.Word, Width = 320 };
        var apply = new Button { Text = "Save" };
        var cancel = new Button { Text = "Cancel" };
        apply.Click += (_, _) =>
        {
            try
            {
                var style = entry.Style with { Fill = colour.Value.ToHex(false), Opacity = opacity.Value, Designed = designed.Checked == true,
                    Appearance = entry.Style.Appearance with { TitleCase = titleCase.SelectedKey!, Text = autoText.Checked == true ? null : textColour.Value.ToHex(false) } };
                var result = edit.Save(name.Text, style, defaults.Checked == true);
                if (result.Complete) dialog.Close(true);
                else error.Text = "Group updated. Future defaults could not be saved: " + result.DefaultsError
                    + " Retry Save, or turn off the future-groups option to finish.";
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.DefaultButton = apply; dialog.AbortButton = cancel;
        var layout = new DynamicLayout { Spacing = new Size(10, 12) };
        layout.AddRow(new Label { Text = "Group name" }); layout.AddRow(name);
        layout.AddRow(new Label { Text = "Letter case" }, titleCase);
        layout.AddRow(new Label { Text = "Background" }, colour);
        layout.AddRow(new Label { Text = "Opacity %" }, opacity);
        layout.AddRow(new Label { Text = "Title colour" }, textColour); layout.AddRow(autoText);
        layout.AddRow(new Label { Text = "Choose a title colour to switch to custom.", TextColor = SystemColors.DisabledText });
        layout.AddRow(designed); layout.AddRow(defaults); layout.AddRow(error);
        layout.AddRow(null, cancel, apply); dialog.Content = layout;
        dialog.Shown += (_, _) => { name.Focus(); name.SelectAll(); };
        return dialog;
    }
}
