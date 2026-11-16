using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

static void InspectRuntime(MetadataReader reader, int rhino)
{
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
    if (reader.GetAssemblyDefinition().Version != new Version(0, 5, 0, 8))
        throw new Exception("Incorrect assembly version");
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
