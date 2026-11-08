using Eto.Forms;
using Eto.Drawing;
using Grasshopper.Kernel.Special;

namespace Margin.Native;

// Edits the native group, with the same undo/revision checks as Studio.
internal static class GroupEditor
{
    public static void Show(GH_Group group)
    {
        var doc = group.OnPingDocument();
        if (doc is null) return;
        var snapshot = Groups.Snapshot(doc);
        var entry = snapshot.Items.First(g => g.Id == group.InstanceGuid.ToString());
        using var dialog = new Dialog<bool> { Title = "Edit group", Padding = new Padding(20), Resizable = false };
        var name = new TextBox { Text = entry.Name, PlaceholderText = "Group name", Width = 320 };
        var colour = new ColorPicker { Value = Color.Parse(entry.Style.Fill) };
        var autoText = new CheckBox { Text = "Automatic title contrast", Checked = entry.Style.Appearance.Text is null };
        var textColour = new ColorPicker { Value = Color.Parse(entry.Style.Appearance.Text ?? "#F4F3F0"), Enabled = entry.Style.Appearance.Text is not null };
        autoText.CheckedChanged += (_, _) => textColour.Enabled = autoText.Checked != true;
        var opacity = new NumericStepper { MinValue = 0, MaxValue = 100, Value = entry.Style.Opacity };
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
                    Appearance = entry.Style.Appearance with { Text = autoText.Checked == true ? null : textColour.Value.ToHex(false) } };
                Groups.Apply(doc, snapshot.Document!, entry.Id, entry.Revision, name.Text, style);
                if (defaults.Checked == true) Groups.SaveDefaults(style);
                dialog.Close(true);
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.DefaultButton = apply; dialog.AbortButton = cancel;
        var layout = new DynamicLayout { Spacing = new Size(10, 12) };
        layout.AddRow(new Label { Text = "Group name" }); layout.AddRow(name);
        layout.AddRow(new Label { Text = "Background" }, colour);
        layout.AddRow(new Label { Text = "Opacity %" }, opacity);
        layout.AddRow(autoText); layout.AddRow(new Label { Text = "Title colour" }, textColour);
        layout.AddRow(designed); layout.AddRow(defaults); layout.AddRow(error);
        layout.AddRow(null, cancel, apply); dialog.Content = layout;
        dialog.Shown += (_, _) => { name.Focus(); name.SelectAll(); };
        dialog.ShowModal(Runtime.Owner);
    }
}
