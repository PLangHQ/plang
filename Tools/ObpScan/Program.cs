using Microsoft.Build.Locator;
using ObpScan;

// ObpScan — emits the per-member OBP table so a scan is an artifact, not an impression.
// Each concept owns its own behaviour (the principle the tool enforces): a MemberName judges its
// own smell, a Member answers who calls it and whether it's misplaced, a TypeScan renders itself.
// Usage: dotnet run --project Tools/ObpScan -- <type-substring> [project.csproj]

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: ObpScan <type-substring> [project.csproj]");
    Console.Error.WriteLine("  matches 'namespace.TypeName' (e.g. app.goal.steps.step.actions)");
    return 1;
}

MSBuildLocator.RegisterDefaults();

using var codebase = await Codebase.Load(args.Length > 1 ? args[1] : "PLang/PLang.csproj");
var scans = await codebase.Find(args[0]);

if (scans.Count == 0) { Console.Error.WriteLine($"no type matched '{args[0]}'"); return 1; }
if (scans.Count > 5)
{
    Console.Error.WriteLine($"'{args[0]}' matched {scans.Count} types — narrow it:");
    foreach (var s in scans.Take(20)) Console.Error.WriteLine("  " + s.FullName);
    return 1;
}

foreach (var scan in scans)
    Console.WriteLine(await scan.Render());

return 0;
