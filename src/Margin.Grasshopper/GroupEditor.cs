using Eto.Forms;
using Eto.Drawing;
using Margin.Core;
#if MARGIN_GH2
using NativeGroup = Grasshopper2.SpecialObjects.GroupObject;
using Runtime = Margin.GH2.Runtime;
using Grasshopper2.UI;
#else
using NativeGroup = Grasshopper.Kernel.Special.GH_Group;
using Grasshopper.GUI.Canvas;
#endif

namespace Margin.Native;

// Edits the native group, with the same undo/revision checks as Studio.
internal static class GroupEditor
{
    public static void Show(NativeGroup group)
    {
#if MARGIN_GH2
        if (group.Document is null || !ReferenceEquals(group.Document, Editor.Instance?.Canvas?.Document)) return;
#else
        if (group.OnPingDocument() is null || !ReferenceEquals(group.OnPingDocument(), Grasshopper.Instances.ActiveCanvas?.Document)) return;
#endif
        using var dialog = BuildDialog(group, activeOnly: true);
        dialog?.ShowModal(Runtime.Owner);
    }
    internal static Dialog<bool>? CreateDialog(NativeGroup group) => BuildDialog(group, activeOnly: false);
    static Dialog<bool>? BuildDialog(NativeGroup group, bool activeOnly)
    {
#if MARGIN_GH2
        var doc = group.Document;
        var id = group.InstanceId;
#else
        var doc = group.OnPingDocument();
        var id = group.InstanceGuid;
#endif
        if (doc is null) return null;
        var snapshot = Groups.Snapshot(doc);
        var entry = snapshot.Items.FirstOrDefault(g => g.Id == id.ToString());
        if (entry is null) return null;
        var edit = activeOnly ? Groups.BeginEdit(snapshot.Document!, entry.Id, entry.Revision)
            : Groups.BeginEdit(doc, snapshot.Document!, entry.Id, entry.Revision);
        var dialog = new Dialog<bool> { Title = "Edit group", Padding = new Padding(20), Resizable = false };
        var name = new TextBox { Text = entry.Name, PlaceholderText = "Group name", Width = 320 };
        var titleCase = new DropDown { ID = "group-title-case", Width = 180 };
        foreach (var option in GroupAppearancePolicy.TitleCases) titleCase.Items.Add(new ListItem { Key = option.Key, Text = option.Value });
        titleCase.SelectedKey = entry.Style.Appearance.TitleCase;
        var colour = new ColorPicker { ID = "group-fill", Value = Color.Parse(entry.Style.Fill) };
        var autoText = new CheckBox { ID = "group-title-auto", Text = "Automatic title contrast", Checked = entry.Style.Appearance.Text is null };
#if MARGIN_GH2
        var canvasHex = Editor.Instance?.Canvas?.Skin.Canvas.Background.ToHex(false) ?? "#D4D0C8";
        string TitleInk(GroupAppearance a, string fill, double opacity) =>
            GroupAppearancePolicy.Resolve(a, fill, opacity, canvasHex).Text;
#else
        var canvasColour = GH_Skin.canvas_back;
        var canvasHex = $"#{canvasColour.R:X2}{canvasColour.G:X2}{canvasColour.B:X2}";
        string TitleInk(GroupAppearance a, string fill, double opacity) => Groups.TitleColour(group, a, fill, opacity, canvasHex);
#endif
        var resolved = TitleInk(entry.Style.Appearance, entry.Style.Fill, entry.Style.Opacity);
        var textColour = new ColorPicker { ID = "group-title-colour", Value = Color.Parse(resolved) };
        var updatingColour = false;
        textColour.ValueChanged += (_, _) => { if (!updatingColour) autoText.Checked = false; };
        var opacity = new NumericStepper { MinValue = 0, MaxValue = 100, Value = entry.Style.Opacity };
        void UpdateAutomaticColour()
        {
            if (autoText.Checked != true) return;
            var appearance = entry.Style.Appearance with { Text = null };
            var next = TitleInk(appearance, colour.Value.ToHex(false), opacity.Value);
            updatingColour = true;
            try { textColour.Value = Color.Parse(next); }
            finally { updatingColour = false; }
        }
        autoText.CheckedChanged += (_, _) => UpdateAutomaticColour();
        colour.ValueChanged += (_, _) => UpdateAutomaticColour();
        opacity.ValueChanged += (_, _) => UpdateAutomaticColour();
        var designed = new CheckBox { Text = "Margin styling", Checked = entry.Style.Designed };
        var showTitle = new CheckBox { ID = "group-show-title", Text = "Show title", Checked = entry.Style.Appearance.ShowTitle, Enabled = entry.Style.Designed };
        designed.CheckedChanged += (_, _) => showTitle.Enabled = designed.Checked == true;
        var defaults = new CheckBox { Text = "Use this appearance for future groups" };
        var error = new Label { TextColor = Colors.IndianRed, Wrap = WrapMode.Word, Width = 320 };
        var apply = new Button { Text = "Save" };
        var cancel = new Button { Text = "Cancel" };
        apply.Click += (_, _) =>
        {
            try
            {
                var style = entry.Style with { Fill = colour.Value.ToHex(false), Opacity = opacity.Value, Designed = designed.Checked == true,
                    Appearance = entry.Style.Appearance with { ShowTitle = showTitle.Checked == true, TitleCase = titleCase.SelectedKey!, Text = autoText.Checked == true ? null : textColour.Value.ToHex(false) } };
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
        layout.AddRow(designed); layout.AddRow(showTitle);
        layout.AddRow(new Label { Text = "Letter case" }, titleCase);
        layout.AddRow(new Label { Text = "Background" }, colour);
        layout.AddRow(new Label { Text = "Opacity %" }, opacity);
        layout.AddRow(new Label { Text = "Title colour" }, textColour); layout.AddRow(autoText);
        layout.AddRow(new Label { Text = "Choose a title colour to switch to custom.", TextColor = SystemColors.DisabledText });
        layout.AddRow(defaults); layout.AddRow(error);
        layout.AddRow(null, cancel, apply); dialog.Content = layout;
        dialog.Shown += (_, _) => { name.Focus(); name.SelectAll(); };
        return dialog;
    }
}
