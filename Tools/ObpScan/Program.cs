using Microsoft.Build.Locator;
using ObpScan;

// ObpScan — emits the per-member OBP table so a scan is an artifact, not an impression.
// Each concept owns its own behaviour (the principle the tool enforces): a MemberName judges its
// own smell, a Member answers who calls it and whether it's misplaced, a TypeScan renders itself.
// Usage: dotnet run --project Tools/ObpScan -- <type-substring> [project.csproj]
//        dotnet run --project Tools/ObpScan -- --tree <folder> [project.csproj]   the shape flags of every type under it

if (args.Length == 0 || (args[0] == "--tree" && args.Length < 2))
{
    Console.Error.WriteLine("usage: ObpScan <type-substring> [project.csproj]");
    Console.Error.WriteLine("  matches 'namespace.TypeName' (e.g. app.goal.steps.step.actions)");
    Console.Error.WriteLine("       ObpScan --tree <folder> [project.csproj]");
    Console.Error.WriteLine("  every member under the folder that carries a shape flag (no caller sets)");
    return 1;
}

MSBuildLocator.RegisterDefaults();

if (args[0] == "--tree")
{
    using var project = await Codebase.Load(args.Length > 2 ? args[2] : "PLang/PLang.csproj");
    var types = await project.Under(args[1]);
    var lines = types.SelectMany(type => type.Shapes(Directory.GetCurrentDirectory())).Order(StringComparer.Ordinal).ToList();
    foreach (var line in lines) Console.WriteLine(line);
    Console.WriteLine($"\n{lines.Count} members with a shape flag in {types.Count} types under {args[1]}: "
        + $"{lines.Count(l => l.Contains("RAW-COLLECTION"))} RAW-COLLECTION, {lines.Count(l => l.Contains("GET-TWIN"))} GET-TWIN, "
        + $"{lines.Count(l => l.Contains("MUTABLE-COLLECTION"))} MUTABLE-COLLECTION");
    return 0;
}

using var codebase = await Codebase.Load(args.Length > 1 ? args[1] : "PLang/PLang.csproj");
var scans = await codebase.Find(args[0]);

if (scans.Count == 0) { Console.Error.WriteLine($"no type matched '{args[0]}'"); return 1; }
if (scans.Count > 40)
{
    Console.Error.WriteLine($"'{args[0]}' matched {scans.Count} types — too broad, narrow it:");
    foreach (var s in scans.Take(40)) Console.Error.WriteLine("  " + s.FullName);
    return 1;
}

foreach (var scan in scans)
    Console.WriteLine(await scan.Render());

return 0;
