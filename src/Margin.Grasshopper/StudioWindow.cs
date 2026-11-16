using Margin.Core;
using Eto.Forms;

namespace Margin.Native;

// Native window policy is isolated so the Windows build never loads AppKit.
internal static class StudioWindow
{
    internal static void Prepare(Form form)
    {
#if MARGIN_MAC && (MARGIN_RHINO8 || MARGIN_RHINO7)
        // Rhino 8 supplies different macOS bindings across runtime updates.
        var window = form.ControlObject;
        var behaviour = window.GetType().GetProperty("CollectionBehavior");
        if (behaviour is not null)
            behaviour.SetValue(window, Enum.Parse(behaviour.PropertyType, "FullScreenAuxiliary, MoveToActiveSpace"));
#elif MARGIN_MAC
        if (form.ControlObject is AppKit.NSWindow window)
            window.CollectionBehavior = AppKit.NSWindowCollectionBehavior.FullScreenAuxiliary
                | AppKit.NSWindowCollectionBehavior.MoveToActiveSpace;
#endif
    }
    internal static void KeepOnScreen(Form form)
    {
#if MARGIN_MAC && !MARGIN_RHINO8 && !MARGIN_RHINO7
        if (form.ControlObject is AppKit.NSWindow shown && shown.Screen is not null)
        {
            var area = shown.Screen.VisibleFrame;
            var frame = shown.Frame;
            shown.SetFrameOrigin(new CoreGraphics.CGPoint(
                RuntimeSupport.Clamp((double)frame.X, (double)area.X, Math.Max((double)area.X, (double)(area.Right - frame.Width))),
                RuntimeSupport.Clamp((double)frame.Y, (double)area.Y, Math.Max((double)area.Y, (double)(area.Bottom - frame.Height)))));
        }
#else
        var area = form.Screen.WorkingArea;
        var bounds = form.Bounds;
        form.Location = new Eto.Drawing.Point(
            (int)RuntimeSupport.Clamp(bounds.X, area.Left, Math.Max(area.Left, area.Right - bounds.Width)),
            (int)RuntimeSupport.Clamp(bounds.Y, area.Top, Math.Max(area.Top, area.Bottom - bounds.Height)));
#endif
    }
}
