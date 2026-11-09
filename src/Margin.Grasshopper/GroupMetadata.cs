using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace Margin.Native;

// GH_Group inherits serialised object values but exposes their accessors only to
// subclasses. Bind the documented accessors once without replacing native groups.
static class GroupMetadata
{
    const string Key = "Margin.GroupStyle";
    static readonly Func<GH_DocumentObject, string, string, string> get = Accessor("GetValue")
        .CreateDelegate<Func<GH_DocumentObject, string, string, string>>();
    static readonly Action<GH_DocumentObject, string, string> set = Accessor("SetValue")
        .CreateDelegate<Action<GH_DocumentObject, string, string>>();

    static MethodInfo Accessor(string name) => typeof(GH_DocumentObject).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string), typeof(string)], null)
        ?? throw new NotSupportedException("This Grasshopper version cannot store portable Margin group appearance.");

    public static string Read(GH_Group group) => get(group, Key, "");
    public static void Write(GH_Group group, string value) => set(group, Key, value);
}
