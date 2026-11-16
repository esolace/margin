using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

if (args.Length < 2) throw new ArgumentException("Pass the built Margin adapter and Rhino SDK dependency directory.");
var adapterPath = Path.GetFullPath(args[0]);
var folders = new[] { Path.GetDirectoryName(adapterPath)! }.Concat(args.Skip(1).Select(Path.GetFullPath)).ToArray();
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    foreach (var folder in folders)
    {
        var path = Path.Combine(folder, name.Name + ".dll");
        if (File.Exists(path)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    }
    return null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(adapterPath);
var type = assembly.GetType("Margin.Native.Inspector", throwOnError: true)!;
var eto = Assembly.Load(assembly.GetReferencedAssemblies().Single(name => name.Name == "Eto"));
// Reproduce a queued callback after disposal with the actual Eto disposed state.
// No native window or Rhino process is created by this regression check.
var form = RuntimeHelpers.GetUninitializedObject(type);
eto.GetType("Eto.Widget")!.GetProperty("IsDisposed")!.SetValue(form, true);
var flags = BindingFlags.Instance | BindingFlags.NonPublic;
type.GetField("<Ready>k__BackingField", flags)!.SetValue(form, true);
var context = new CaptureContext();
var previousContext = SynchronizationContext.Current;
SynchronizationContext.SetSynchronizationContext(context);
try
{
    type.GetMethod("SyncGroupSelection", flags)!.Invoke(form, new object?[] { null, EventArgs.Empty });
    if (context.Error is not null) throw new Exception("A queued Studio timer escaped after disposal.", context.Error);
    var script = (Task<string>)type.GetMethod("InspectAsync")!.Invoke(form, new object[] { "return 1;" })!;
    if (await script != string.Empty) throw new Exception("A disposed Studio attempted browser execution.");
    var message = (Task)type.GetMethod("HandleMessage", flags)!.Invoke(form, new object[] { "invalid message" })!;
    await message;
    Console.WriteLine("PASS disposed Studio ignores queued timer, browser and message callbacks");
}
finally { SynchronizationContext.SetSynchronizationContext(previousContext); }

sealed class CaptureContext : SynchronizationContext
{
    public Exception? Error { get; private set; }
    public override void Post(SendOrPostCallback callback, object? state)
    {
        try { callback(state); }
        catch (Exception exception) { Error = exception; }
    }
}
