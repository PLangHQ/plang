# packages — coder v1 plan

Bring the NuGet packages up to date (architect row 1, Ingi's yes). No new behaviour: only packages, plus the code their APIs force.

## Order: one cause per commit, each followed by the sweep and `plang --test` (PlangConsole rebuilt)

1. SixLabors.ImageSharp 2.1.13 → 4.1.3. The screen module's ImageSharp code (`module/screen/type/screen/{view,window}/this.cs`) and the image type's `DetectFormat` (`type/item/image/this.cs`) follow the 4.x API.
2. Microsoft.Data.Sqlite, Microsoft.AspNetCore.WebUtilities, System.Runtime.Caching → 10.0.13.
3. Fluid.Core → 2.40.0.
4. Each major alone, one commit each:
   - NSec.Cryptography → 26.4.0 (every signing and identity test must stay green)
   - Nethereum.Util → 7.0.0 (the keccak tests; if it no longer pulls Newtonsoft 11, the direct Newtonsoft.Json pin goes)
   - TUnit, TUnit.Core, TUnit.Assertions → 1.76.0
   - NSubstitute → 6.2.0
   - coverlet.collector → 10.1.0
5. Roslyn (PLang.Generators' Microsoft.CodeAnalysis.CSharp 4.13.0): the SDK here (10.0.402) ships Roslyn 5.9.0. Report that to the architect, and move it only as ruled.

## Checks
- `dotnet list package --vulnerable --include-transitive`, per project, the full table: before (`vulnerable-before.txt`) and after.
- Baseline (`baseline-tests.md`): the sweep's reds stay runtime2's (Wire's six); `plang --test` stays at 298 of 303.
- A package whose upgrade breaks an API beyond a mechanical fix: stop, and tell the architect what broke.
- `Tools/Bench/run.sh` after the last commit.
