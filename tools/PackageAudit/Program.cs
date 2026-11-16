using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

static void InspectRuntime(MetadataReader reader, int rhino)
{
    if (rhino == 7)
    {
        var refs = reader.AssemblyReferences.Select(reader.GetAssemblyReference).ToArray();
        if (!refs.Any(r => reader.GetString(r.Name) == "mscorlib" && r.Version.Major == 4)
            || refs.Any(r => reader.GetString(r.Name) == "System.Runtime" && r.Version.Major >= 8))
            throw new Exception("Rhino 7 payload must target the older Framework runtime");
        return;
    }
    var runtime = reader.AssemblyReferences.Select(reader.GetAssemblyReference)
        .Single(r => reader.GetString(r.Name) == "System.Runtime");
    if (runtime.Version.Major != (rhino == 8 ? 8 : 10))
        throw new Exception("Payload requires an incompatible .NET runtime");
}

static void InspectCore(Stream stream, int rhino)
{
    using var pe = new PEReader(stream);
    InspectRuntime(pe.GetMetadataReader(), rhino);
}

static void Inspect(Stream stream, bool windows, bool gh2, int rhino = 9, int? minor = null)
{
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();
    var refs = reader.AssemblyReferences.Select(h => reader.GetString(reader.GetAssemblyReference(h).Name)).ToHashSet();
    if (!refs.Contains(gh2 ? "Grasshopper2" : "Grasshopper") || refs.Contains(gh2 ? "Grasshopper" : "Grasshopper2"))
        throw new Exception("Incorrect host dependency");
    var references = reader.AssemblyReferences.Select(reader.GetAssemblyReference).ToArray();
    var host = references.Single(r => reader.GetString(r.Name) == "RhinoCommon");
    if (host.Version.Major != rhino || (minor.HasValue && host.Version.Minor != minor.Value))
        throw new Exception("Payload targets a different Rhino version");
    InspectRuntime(reader, rhino);
    if (windows && refs.Any(r => r.Contains("macOS") || r.Contains("AppKit") || r.Contains("CoreGraphics")))
        throw new Exception("Windows payload references Apple libraries");
    if (!reader.ManifestResources.Any(h => reader.GetString(reader.GetManifestResource(h).Name) == "Margin.Studio.html"))
        throw new Exception("Studio resource is missing");
    if (reader.GetAssemblyDefinition().Version != new Version(0, 5, 0, rhino == 7 ? 9 : 8))
        throw new Exception("Incorrect assembly version");
}

if (args.Length == 3 && args[0] == "--rhino7")
{
    var path = args[1];
    using var archive = File.Exists(path) ? ZipFile.OpenRead(path) : null;
    Stream Open(string name)
    {
        if (archive is null) return File.OpenRead(Path.Combine(path, name));
        var stream = new MemoryStream();
        using var entry = archive.GetEntry(name)!.Open();
        entry.CopyTo(stream); stream.Position = 0;
        return stream;
    }
    var dependencies = new[] { "Microsoft.Bcl.AsyncInterfaces", "System.Buffers", "System.Memory", "System.Numerics.Vectors",
        "System.Runtime.CompilerServices.Unsafe", "System.Text.Encodings.Web", "System.Text.Json", "System.Threading.Tasks.Extensions", "System.ValueTuple" };
    var allowed = new HashSet<string> { "Margin.gha", "Margin.Core.dll", "manifest.yml", "README.md", "LICENSE", "THIRD-PARTY-NOTICES.txt", "icon.png", "Margin-source.zip" };
    allowed.UnionWith(dependencies.Select(name => name + ".dll"));
    var files = archive is null
        ? Directory.GetFileSystemEntries(path).Where(p => !p.EndsWith(".yak")).Select(Path.GetFileName).ToHashSet()
        : archive.Entries.Select(e => e.FullName).ToHashSet();
    if (!files.SetEquals(allowed)) throw new Exception("Incorrect Rhino 7 root payload");
    using (var plugin = Open("Margin.gha")) Inspect(plugin, false, false, 7, 38);
    using (var core = Open("Margin.Core.dll"))
    {
        using var pe = new PEReader(core);
        var reader = pe.GetMetadataReader();
        InspectRuntime(reader, 7);
        if (!reader.ManifestResources.Any(h => reader.GetString(reader.GetManifestResource(h).Name) == "Margin.LegacyUnicode.json"))
            throw new Exception("Legacy Unicode data missing");
    }
    // Every non-framework dependency must be packaged or supplied by Rhino 7.
    var provided = new HashSet<string> { "RhinoCommon", "Rhino.UI", "Grasshopper", "GH_IO", "Eto",
        "mscorlib", "System", "System.Core", "System.Drawing", "System.Windows.Forms", "System.Numerics",
        "System.Runtime.Serialization", "System.Xml", "System.Xml.Linq", "System.Data", "System.Configuration" };
    var identities = new Dictionary<string, Version>();
    foreach (var name in files.Where(n => n.EndsWith(".dll") || n.EndsWith(".gha")))
    {
        using var stream = Open(name);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var assembly = reader.GetAssemblyDefinition();
        identities.Add(reader.GetString(assembly.Name), assembly.Version);
    }
    foreach (var name in files.Where(n => n.EndsWith(".dll") || n.EndsWith(".gha")))
    {
        using var stream = Open(name);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.AssemblyReferences)
        {
            var reference = reader.GetAssemblyReference(handle);
            var target = reader.GetString(reference.Name);
            if (!provided.Contains(target))
            {
                if (!identities.TryGetValue(target, out var version) || version < reference.Version)
                    throw new Exception($"Missing or older dependency: {name} -> {target} {reference.Version}");
                if (version != reference.Version)
                    Console.WriteLine($"NATIVE CHECK PENDING: {name} requests {target} {reference.Version}; bundled {version}. Verify Mono binding.");
            }
        }
    }
    using (var stream = File.OpenRead(args[2]))
    using (var pe = new PEReader(stream))
    {
        var reader = pe.GetMetadataReader();
        var type = reader.TypeDefinitions.Select(reader.GetTypeDefinition).Single(t => reader.GetString(t.Name) == "GH_GroupAttributes");
        foreach (var (name, count, result) in new[] { ("IsOutlineValid", 0, SignatureTypeCode.Boolean), ("RenderGroupShape", 1, SignatureTypeCode.Void) })
        {
            var method = type.GetMethods().Select(reader.GetMethodDefinition).Single(m => reader.GetString(m.Name) == name);
            var signature = reader.GetBlobReader(method.Signature);
            var header = signature.ReadSignatureHeader();
            if (!header.IsInstance || header.IsGeneric || signature.ReadCompressedInteger() != count || signature.ReadSignatureTypeCode() != result)
                throw new Exception("Unsupported native group method: " + name);
            if (count == 1)
            {
                if (signature.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle) throw new Exception("Expected native canvas parameter");
                var handle = signature.ReadTypeHandle();
                var parameter = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                if (reader.GetString(parameter.Name) != "GH_Canvas") throw new Exception("Unexpected native canvas parameter");
            }
        }
    }
    using (var stream = Open("Margin-source.zip"))
    using (var source = new ZipArchive(stream))
    {
        if (!source.Entries.Any(e => e.FullName == "src/Margin.Core/LegacyUnicode.json")
            || !source.Entries.Any(e => e.FullName == "tools/build_rhino7.py")
            || source.Entries.Any(e => e.FullName.StartsWith("artifacts/") || e.FullName.Contains("/bin/") || e.FullName.Contains("/obj/")))
            throw new Exception("Incomplete or contaminated candidate source archive");
    }
    Console.WriteLine("PASS Rhino 7 exact payload, Framework target, dependency presence, Studio, Unicode, native group methods and source archive");
    if (archive is not null) Console.WriteLine("SHA256 " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
    return;
}

if (args.Length > 3 && args[0] == "--assemblies")
{
    var rhino = int.Parse(args[1]);
    foreach (var path in args.Skip(3))
    {
        using var stream = File.OpenRead(path);
        Inspect(stream, args[2] == "win", path.EndsWith(".rhp"), rhino);
        using var core = File.OpenRead(Path.Combine(Path.GetDirectoryName(path)!, "Margin.Core.dll"));
        InspectCore(core, rhino);
    }
    Console.WriteLine("PASS adapter host, runtime, platform and Studio dependencies");
    return;
}
if (args.Length > 0 && args[0] == "--gh2")
{
    foreach (var path in args.Skip(1))
    {
        using var stream = File.OpenRead(path);
        Inspect(stream, path.Contains("-win"), true);
        if (!File.Exists(path + ".grasshopper-only")) throw new Exception("Missing GH2 loader marker");
        Console.WriteLine("PASS GH2 assembly and platform audit: " + Path.GetFileName(path));
    }
    return;
}
foreach (var path in args)
{
    using var archive = ZipFile.OpenRead(path);
    var target = Regex.Match(Path.GetFileName(path), @"-rh(8|9)_(\d+)-(win|mac)\.yak$");
    if (!target.Success) throw new Exception("Unsupported distribution target");
    var rhino = int.Parse(target.Groups[1].Value);
    var minor = int.Parse(target.Groups[2].Value);
    var runtime = rhino == 8 ? "net8.0" : "net10.0";
    var allowed = new HashSet<string> { "LICENSE", "Margin-source.zip", "manifest.yml", "icon.png", "README.md", runtime + "/Margin.gha", runtime + "/Margin.Core.dll" };
    var adapters = new List<string> { "Margin.gha" };
    if (rhino == 9)
    {
        allowed.Add(runtime + "/Margin.GH2.rhp");
        allowed.Add(runtime + "/Margin.GH2.rhp.grasshopper-only");
        adapters.Add("Margin.GH2.rhp");
    }
    var files = archive.Entries.Where(e => !e.FullName.EndsWith('/')).Select(e => e.FullName).ToHashSet();
    if (!files.SetEquals(allowed)) throw new Exception("Unexpected or missing package content: " + path);
    foreach (var name in adapters)
    {
        using var stream = new MemoryStream();
        archive.GetEntry(runtime + "/" + name)!.Open().CopyTo(stream); stream.Position = 0;
        Inspect(stream, path.EndsWith("-win.yak"), name.EndsWith(".rhp"), rhino, minor);
    }
    using (var stream = new MemoryStream())
    {
        archive.GetEntry(runtime + "/Margin.Core.dll")!.Open().CopyTo(stream); stream.Position = 0;
        InspectCore(stream, rhino);
    }
    using var sourceStream = new MemoryStream();
    archive.GetEntry("Margin-source.zip")!.Open().CopyTo(sourceStream); sourceStream.Position = 0;
    using var source = new ZipArchive(sourceStream);
    if (!source.Entries.Any(e => e.FullName == "LICENSE") || !source.Entries.Any(e => e.FullName == "src/Margin.Grasshopper2/Labels.cs"))
        throw new Exception("Source archive is incomplete");
    if (source.Entries.Any(e => e.FullName.StartsWith("artifacts/") || e.FullName.Contains("/bin/") || e.FullName.Contains("/obj/")))
        throw new Exception("Private/build files in source archive");
    Console.WriteLine("PASS " + Path.GetFileName(path) + ": exact payload, host/runtime, embedded Studio, version, source and platform checks");
    Console.WriteLine("SHA256 " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
}
