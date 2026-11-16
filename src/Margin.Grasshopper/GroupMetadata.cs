using Margin.Core;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace Margin.Native;

// GH_Group inherits serialised object values but exposes their accessors only to
// subclasses. Bind the documented accessors once without replacing native groups.
static class GroupMetadata
{
    const string Key = "Margin.GroupStyle";
    static readonly Func<GH_DocumentObject, string, string, string> get = (Func<GH_DocumentObject, string, string, string>)Accessor("GetValue")
        .CreateDelegate(typeof(Func<GH_DocumentObject, string, string, string>));
    static readonly Action<GH_DocumentObject, string, string> set = (Action<GH_DocumentObject, string, string>)Accessor("SetValue")
        .CreateDelegate(typeof(Action<GH_DocumentObject, string, string>));

    static MethodInfo Accessor(string name) => typeof(GH_DocumentObject).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string), typeof(string)], null)
        ?? throw new NotSupportedException(Localization.Text("ui.this_grasshopper_version_cannot_store_portable_margin_group_appearance"));

    public static string Read(GH_Group group) => get(group, Key, "");
    public static void Write(GH_Group group, string value) => set(group, Key, value);
}
