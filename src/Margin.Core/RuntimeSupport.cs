namespace Margin.Core;

// Keep older-host differences out of appearance and persistence policy.
public static class RuntimeSupport
{
    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    public static double Clamp(double value, double min, double max)
    {
        if (min > max) throw new ArgumentException("Minimum exceeds maximum.");
        return Math.Min(max, Math.Max(min, value));
    }
    public static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;
    public static void ReplaceFile(string temporary, string destination)
    {
#if NETFRAMEWORK
        // Both paths are siblings on the same volume. Never delete the original
        // first: a failed replacement must leave the saved preferences intact.
        if (File.Exists(destination)) File.Replace(temporary, destination, null);
        else File.Move(temporary, destination);
#else
        File.Move(temporary, destination, true);
#endif
    }
}
