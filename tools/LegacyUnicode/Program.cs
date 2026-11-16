using System.Globalization;
using System.Text;
using System.Text.Json;

// Snapshot the modern host's scalar casing/categories for Rhino 7's older Mono.
// Never use UTF-16 char casing: it changes supplementary-letter title behavior.
var cases = new List<int[]>();
var ranges = new List<int[]>();
for (var value = 0; value <= 0x10ffff; value++)
{
    if (!Rune.IsValid(value)) continue;
    var rune = new Rune(value);
    int upper = Rune.ToUpperInvariant(rune).Value, lower = Rune.ToLowerInvariant(rune).Value;
    if (value != upper || value != lower) cases.Add([value, upper, lower]);
    int category = (int)Rune.GetUnicodeCategory(rune);
    if (ranges.Count > 0 && ranges[^1][1] == value - 1 && ranges[^1][2] == category)
        ranges[^1][1] = value;
    else ranges.Add([value, value, category]);
}
var text = JsonSerializer.Serialize(new { Cases = cases, Ranges = ranges }) + "\n";
var path = args[0];
if (args.Contains("--check"))
{
    if (File.ReadAllText(path) != text) throw new Exception("Legacy Unicode data differs from this runtime. Review and regenerate deliberately.");
}
else File.WriteAllText(path, text);
Console.WriteLine($"Verified {cases.Count} scalar mappings and {ranges.Count} category ranges.");
