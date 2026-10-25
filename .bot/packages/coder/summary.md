# packages — coder summary

**Version:** v1

## What this is
PLang's NuGet packages brought up to date: one package per commit, each followed by the C# sweep and `plang --test`, with no new behaviour. The architect's row 1 sets the order; its rulings are packages 1 and 3.

## What was done
| package | from → to | commit | code it forced |
|---|---|---|---|
| SixLabors.ImageSharp | 2.1.13 → 4.1.3 | ab9459746 | none in Debug; **blocks every non-Debug build without a licence key** (see Blocker) |
| Microsoft.Data.Sqlite, Microsoft.AspNetCore.WebUtilities, System.Runtime.Caching | 9.0.x → 10.0.13 | a14891bdf | none |
| Fluid.Core | 2.31.0 → 2.40.0 | 248f5cb8f, then 57255ca9e | Fluid 2.40 asks the file provider for an include's name as written first, then with `.liquid`. The provider (`PLang/app/module/ui/code/Fluid.cs`) now keys each include by one rule (`Key(name)`: the name with `.liquid` added when it has none), so either ask finds the one row. 57255ca9e also left the file better (boy scout rule, packages 3): errors answered whole, a typed catch, the goal's answer written through the template's value writer, files read through `path.Bytes`, verb+noun and creative names gone. |
| NSec.Cryptography | 24.4.0 → 26.4.0 | 1422e4b43 | none; the real Ed25519 classes are green |
| Nethereum.Util | 4.29.0 → 7.0.0 | ed277be9a | none; then removed entirely (next row) |
| Nethereum + Newtonsoft.Json → BouncyCastle.Cryptography 2.7.0 | — | a8ed10d68 | Ingi asked for Newtonsoft to go. It came only through Nethereum, which PLang used for one hash. Keccak-256 is now `KeccakDigest(256)`, fed as bytes come (`PLang/app/module/crypto/type/hash/kind/keccak256/this.cs`). Known vectors are pinned for "abc" and the empty input. |
| TUnit, .Core, .Assertions | 1.13.11 → 1.76.0 | c61ab7cde | none; exposed an older race (next row) |
| (race fix) | — | 67a1f820c | `action.Return` (`PLang/app/goal/step/action/this.Schema.cs`) set its "computed" flag before its value, and catalog rows are shared across apps. It now publishes the value before the flag. |
| NSubstitute | 5.3.0 → 6.2.0 | 299bf4997 | none |
| coverlet.collector | 6.0.4 → 10.1.0 | 6e701070f | none |
| Microsoft.CodeAnalysis.CSharp (Roslyn) | 4.13.0 | — | stays on purpose (packages 3): a generator references the lowest Roslyn it needs. |

Vulnerable list (`dotnet list package --vulnerable --include-transitive`, per project, the whole table): before, ImageSharp 2.1.13 (3 High, 2 Moderate) in every project (`v1/vulnerable-before.txt`); after, none in any project (`v1/vulnerable-after.txt`).

Tests after each commit: the sweep's reds are runtime2's (Wire's six snapshot tests), and `plang --test` is 298/303 (BareLiteralJudgedByForm fails, 3 stale, 1 skipped).

## Blocker
ImageSharp 4.x's `build/SixLabors.ImageSharp.targets` runs a licence check before compiling. With `ContinueOnError` only for `Debug*` configurations, the gate's own configuration (`./dev.sh full`), a Release build and a published plang all fail with "No Six Labors license found". With ImageSharp held at 2.1.13 (working tree only), the gate passes with analyzers on, so this is the only blocker. Ingi decides: get a licence key and set `SixLaborsLicenseKey` outside the repo, go back to 2.1.13 (no key check, its advisories stay), or replace ImageSharp in the screen module.

## Next
- Ingi's ImageSharp decision, through the architect.
- `Tools/Bench/run.sh` at the final tip, then codeanalyzer.

## Code example
```csharp
// keccak256 takes bytes as they come, like the other hash kinds
var keccak = new Org.BouncyCastle.Crypto.Digests.KeccakDigest(256);
return new digest.@this(this, keccak.BlockUpdate, () =>
{
    var hash = new byte[keccak.GetDigestSize()];
    keccak.DoFinal(hash, 0);
    return hash;
});
```
