using Eto.Forms;

namespace Margin.Native;

// Native window policy is isolated so the Windows build never loads AppKit.
internal static class StudioWindow
{
    internal static void Prepare(Form form)
    {
#if MARGIN_MAC
        if (form.ControlObject is AppKit.NSWindow window)
            window.CollectionBehavior = AppKit.NSWindowCollectionBehavior.FullScreenAuxiliary
                | AppKit.NSWindowCollectionBehavior.MoveToActiveSpace;
#endif
    }
    internal static void KeepOnScreen(Form form)
    {
#if MARGIN_MAC
        if (form.ControlObject is AppKit.NSWindow shown && shown.Screen is not null)
        {
            var area = shown.Screen.VisibleFrame;
            var frame = shown.Frame;
            shown.SetFrameOrigin(new CoreGraphics.CGPoint(
                Math.Clamp((double)frame.X, (double)area.X, Math.Max((double)area.X, (double)(area.Right - frame.Width))),
                Math.Clamp((double)frame.Y, (double)area.Y, Math.Max((double)area.Y, (double)(area.Bottom - frame.Height)))));
        }
#else
        var area = form.Screen.WorkingArea;
        var bounds = form.Bounds;
        form.Location = new Eto.Drawing.Point(
            (int)Math.Clamp(bounds.X, area.Left, Math.Max(area.Left, area.Right - bounds.Width)),
            (int)Math.Clamp(bounds.Y, area.Top, Math.Max(area.Top, area.Bottom - bounds.Height)));
#endif
    }
}
