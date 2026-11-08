using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.IO.Compression;
using System.Security.Cryptography;

static void Inspect(Stream stream, bool windows, bool gh2)
{
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();
    var refs = reader.AssemblyReferences.Select(h => reader.GetString(reader.GetAssemblyReference(h).Name)).ToHashSet();
    if (!refs.Contains(gh2 ? "Grasshopper2" : "Grasshopper") || refs.Contains(gh2 ? "Grasshopper" : "Grasshopper2"))
        throw new Exception("Incorrect host dependency");
    if (windows && refs.Any(r => r.Contains("macOS") || r.Contains("AppKit") || r.Contains("CoreGraphics")))
        throw new Exception("Windows payload references Apple libraries");
    if (!reader.ManifestResources.Any(h => reader.GetString(reader.GetManifestResource(h).Name) == "Margin.Studio.html"))
        throw new Exception("Studio resource is missing");
    if (reader.GetAssemblyDefinition().Version != new Version(0, 5, 0, 5))
        throw new Exception("Incorrect assembly version");
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
    var allowed = new HashSet<string> { "LICENSE", "Margin-source.zip", "manifest.yml", "icon.png", "README.md", "net10.0/Margin.gha", "net10.0/Margin.Core.dll", "net10.0/Margin.GH2.rhp", "net10.0/Margin.GH2.rhp.grasshopper-only" };
    var files = archive.Entries.Where(e => !e.FullName.EndsWith('/')).Select(e => e.FullName).ToHashSet();
    if (!files.SetEquals(allowed)) throw new Exception("Unexpected or missing package content: " + path);
    foreach (var name in new[] { "Margin.gha", "Margin.GH2.rhp" })
    {
        using var stream = new MemoryStream();
        archive.GetEntry("net10.0/" + name)!.Open().CopyTo(stream); stream.Position = 0;
        Inspect(stream, path.EndsWith("-win.yak"), name.EndsWith(".rhp"));
    }
    using var sourceStream = new MemoryStream();
    archive.GetEntry("Margin-source.zip")!.Open().CopyTo(sourceStream); sourceStream.Position = 0;
    using var source = new ZipArchive(sourceStream);
    if (!source.Entries.Any(e => e.FullName == "LICENSE") || !source.Entries.Any(e => e.FullName == "src/Margin.Grasshopper2/Labels.cs"))
        throw new Exception("Source archive is incomplete");
    if (source.Entries.Any(e => e.FullName.StartsWith("artifacts/") || e.FullName.Contains("/bin/") || e.FullName.Contains("/obj/")))
        throw new Exception("Private/build files in source archive");
    Console.WriteLine("PASS " + Path.GetFileName(path) + ": exact payload, both host adapters, embedded Studio, version, source and platform checks");
    Console.WriteLine("SHA256 " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
}
