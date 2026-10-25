# packages — coder summary

**Version:** v1

## What this is
PLang's NuGet packages brought up to date: one package per commit, each followed by the C# sweep and `plang --test`, with no new behaviour. The architect's row 1 sets the order; its rulings are packages 1 and 3.

## What was done
| package | from → to | commit | code it forced |
|---|---|---|---|
| SixLabors.ImageSharp | 2.1.13 → 4.1.3 | ab9459746 | built in Debug only: its licence check fails every other configuration without a key, so it was replaced (next row) |
| ImageSharp → SkiaSharp 4.153.1 + NativeAssets.Linux.NoDependencies | — | 2d37ae5a2 | Ingi's choice (packages 7). The image type uses no image library at all: `ValidateKind` asks its own magic-byte `Sniff`, which now names TIFF too. The view encodes its pixels to PNG and the window decodes PNG frames, both as straight BGRA. `PngTests` round-trips them. |
| (native trims) | — | f75df0c1b | `native.targets`: no package's native `.pdb` in any output. PlangConsole's dev build takes the machine's runtime only, and its path is unchanged. `<RuntimeIdentifiers>` keeps the machine's assets after a publish for another runtime. PlangConsole's Debug bin goes 67 → 28 MB. |
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

## ImageSharp's licence check, resolved
ImageSharp 4.x (and 3.2.0, which backports it) runs a licence check in `build/SixLabors.ImageSharp.targets`, with `ContinueOnError` only for `Debug*`. Every other configuration failed without a key: the gate, Release, a publish. Ingi chose SkiaSharp. Magick.NET was measured partway and dropped. The gate (`./dev.sh full`) is green at f75df0c1b.

Sizes (MB), PlangConsole: Debug bin 67 → 28; publish linux-x64 17 → 27, win-x64 18 → 29, osx-arm64 32.

## Codeanalyzer's review (packages 13)
- F1: a screen's picture has one owner, `PLang/app/module/screen/type/screen/code/Bitmap.cs` (BGRA pixels, width, height). It reads PNG/JPEG into itself (`new Bitmap(bytes)`, FormatException when no image) and writes itself as PNG (`Png`). Shown's canvas and the native window's are a Bitmap, so the `(bgra, w, h)` tuple, `Window.Pixels` and the window's swallowed decode catch are gone. A bad frame line on the decode thread is reported as BadFrame. Shown wasn't made the owner itself: it is a fixed-size canvas with a video decoder and a lock, while a decoded frame takes its size from its bytes.
- F2: the TIFF magics are pinned by FromBytes and ValidateKind tests (each red without the lines).
- F3: three unused usings out of crypto's Default. `Tools/obp-longmethods.py` names `Tools/ObpScan`.
- Per commit, `plang --test '--test.setting.exclude=["llm"]'`: 292 pass, 1 fail (the baseline's), 3 stale, 7 skipped (6 of them llm).

## Next
- Codeanalyzer's re-review.
- `scratch/skiasharp` (pushed) holds the measurement commits; delete it once packages merges.

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
