using Margin.Core;
int passed=0;
void Check(bool ok,string message) {if(!ok)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
var theme=new Theme();
foreach(var preset in Theme.Presets) Check(Theme.FromJson(preset.ToJson())==preset.Normalize(),preset.Name+" round-trips");
var safe=(theme with {FontSize=double.NaN,PaddingX=-100,Fill="bad",Opacity=10000,Radius=double.PositiveInfinity}).Normalize();
Check(safe.FontSize==12 && safe.PaddingX==2 && safe.Fill==theme.Fill && safe.Opacity==100 && safe.Radius==6,"unsafe imported values are bounded");
Check(Theme.FromJson("{\"FontFamily\":null,\"Text\":null}").FontFamily=="Helvetica Neue","null strings fall back");
var component=new Box(100,100,60,40);
var label=LabelLayout.Place(component,100,30,theme,new[]{component});
Check(label==new Box(80,58,100,30),"label centred above component");
Check(LabelLayout.Place(component,100,30,theme,new[]{component,label!.Value})==new Box(80,152,100,30),"collision moves label to other side");
Check(LabelLayout.Place(component,100,30,theme,new[]{component,label.Value,new Box(80,152,100,30)}) is null,"crowded labels hidden");
Check(LabelLayout.Place(component,double.NaN,30,theme,Array.Empty<Box>()) is null,"invalid label dimensions ignored");
Check(SnapEngine.Find(component,new[]{new Box(105,250,60,40)},1).Dx==5,"nearest horizontal snap");
Check(SnapEngine.Find(component,new[]{new Box(110,250,60,40)},1).GuideX is null,"outside tolerance does not snap");
Check(SnapEngine.Find(component,new[]{new Box(110,250,60,40)},.5).Dx==10,"snap radius stays screen-relative");
Check(SnapEngine.Find(component,Array.Empty<Box>(),0)==default,"invalid zoom does not snap");
var temp=Path.Combine(Path.GetTempPath(),"margin-test-"+Guid.NewGuid(),"theme.json");
ThemeStore.Save(temp,Theme.Presets[1]);Check(ThemeStore.Load(temp)==Theme.Presets[1],"theme persists");
ThemeStore.Save(temp,Theme.Presets[2]);Check(ThemeStore.Load(temp)==Theme.Presets[2],"atomic theme replacement");
Directory.Delete(Path.GetDirectoryName(temp)!,true);
var library = Path.Combine(Path.GetTempPath(), "margin-presets-" + Guid.NewGuid(), "presets.json");
var custom = Margin.Presets.PresetStore.Save(library, "My studio", theme with { FontFamily="Avenir", Radius=17, Opacity=42 });
Check(Margin.Presets.PresetStore.Load(library).Single() == custom, "named preset persists all style values");
var second = Margin.Presets.PresetStore.Save(library, "Second", Theme.Presets[4]);
Check(Margin.Presets.PresetStore.Load(library).Count == 2, "saving a second preset preserves the first");
var originalBytes = File.ReadAllText(library);
try { Margin.Presets.PresetStore.Save(library, "MY STUDIO", theme); throw new Exception("Duplicate accepted"); }
catch (InvalidDataException) { Check(File.ReadAllText(library) == originalBytes, "duplicate names cannot overwrite saved styles"); }
var renamed = Margin.Presets.PresetStore.Save(library, "Renamed", custom.Theme, custom.Id);
Check(renamed.Id == custom.Id && renamed.Theme.Radius == 17, "renaming preserves identity and style");
var updated = Margin.Presets.PresetStore.Save(library, "Renamed", theme with { Radius=22 }, custom.Id);
Check(Margin.Presets.PresetStore.Load(library).Single(p => p.Id == custom.Id).Theme.Radius == 22, "updating replaces only the chosen style");
Margin.Presets.PresetStore.Delete(library, custom.Id);
Check(Margin.Presets.PresetStore.Load(library).Single() == second, "deleting preserves other presets");
try { Margin.Presets.PresetStore.Save(library, "Stale", theme, custom.Id); throw new Exception("Stale update accepted"); }
catch (InvalidDataException) { Check(Margin.Presets.PresetStore.Load(library).Single() == second, "stale updates cannot recreate deleted presets"); }
File.WriteAllText(library,"damaged");
try { Margin.Presets.PresetStore.Save(library,"New",theme); throw new Exception("Corruption ignored"); }
catch (System.Text.Json.JsonException) { Check(File.ReadAllText(library)=="damaged","damaged library preserved without silent replacement"); }
Directory.Delete(Path.GetDirectoryName(library)!,true);
Console.WriteLine($"{passed} checks passed.");
