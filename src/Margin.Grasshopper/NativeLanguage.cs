using Margin.Core;
using System.Globalization;
#if MARGIN_GH2
using Runtime = Margin.GH2.Runtime;
#endif

namespace Margin.Native;

internal static class NativeLanguage
{
    internal static string Choice => Localization.Choice;
    internal static string HostLanguage
    {
        get
        {
            try { return CultureInfo.GetCultureInfo(Rhino.ApplicationSettings.AppearanceSettings.LanguageIdentifier).Name; }
            catch (CultureNotFoundException) { return CultureInfo.CurrentUICulture.Name; }
        }
    }
    internal static void Start()
    {
        var choice = "auto";
        try { choice = PreferencesStore.Load(Runtime.PreferencesPath).Language; }
        catch (Exception ex) { Rhino.RhinoApp.WriteLine("Margin language: " + ex.Message); }
        Localization.Use(choice, HostLanguage);
    }
    internal static void Refresh() => Localization.Use(Choice, HostLanguage);
    internal static void Save(string choice)
    {
        PreferencesStore.Update(Runtime.PreferencesPath, preferences => preferences with { Language = choice });
        Localization.Use(choice, HostLanguage);
        Runtime.Apply(Runtime.Current);
    }
    internal static object Snapshot() => new { choice = Choice, host = HostLanguage };
}
