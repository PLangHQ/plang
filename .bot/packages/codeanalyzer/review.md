# packages: review (tip 63a299efb; ObpScan commits 0e8097701, d8190936e, 59ff8bbe4)

Parent runtime2 55302f8a5. Scope: every production file the branch touched, read whole (boy scout), the three ObpScan commits, the tests added, the bench, the per-commit runs. Read only; the only edits to production source were the three announced mutations below, reverted (`git status` clean).

**Verdict: FAIL, three small findings (F1–F3), the coder is next.** Nothing is wrong with a package, a bump or a measured number. F1 and F2 are the branch's own seams in files it touched; F3 is a leftover its own change made.

## Findings (fix before the merge)

### F1. The window's `Pixels`: a stray helper over a raw tuple, whose owner is missing (`window/this.cs:147-162`, older; made `internal` for a test by this branch)
- **Whose verb:** not the window's. `Pixels(byte[])` turns a picture's bytes into straight BGRA, `Decode(string)` (:147) turns a frame line into the same, and the view's `Picture` (`view/this.cs:63-70`) goes the other way, BGRA to PNG. The triple `(byte[] bgra, int w, int h)` is passed by hand through `Pixels`' return, `Decode`'s return, `Window.Offer`'s `Func<string, (byte[] bgra, int w, int h)?>` (`code/Window.cs:110`), its field `decode` (:105), `Present(byte[], int, int)` (:94, called :149 and `window/this.cs:132`), and `Shown` holds the same three (`Pixels`, `Width`, `Height`). That is a **stray helper** (`Pixels` should be a verb of the thing it decodes) and a **flat copy / opened box** (a picture opened into its parts at every hand-off). The missing type is a picture in the screen module's `code/` beside `Shown` and `Qoi` (module-owned, so no locked surface is touched): it holds BGRA, width, height; it is read from bytes (the decode, with the Skia call) and writes itself as a PNG (the view's encode). `Window.Present(picture)`, `Offer`'s decoder returns it, and the view's `Picture` asks it.
- **Why the test seam goes with it:** `Pixels` is not rule 35's case (production calls it), but `internal` was widened only so `PngTests` can skip `Draw`, which needs Win32. The rule's other half is "a test of real behaviour reaches it through the door production uses". With a picture type, both production sites (Draw and Decode) and the test enter through the same door, with no `internal` on the window and no Win32. `PngTests` then tests the type, and the view's `Picture` is its encode.
- **Smaller, same spot:** `Pixels` returns a nullable tuple (`:153`) that is never null (it returns or throws), so both callers' `is { } px` (`:132`, `:148`) test something that cannot be false. A bad frame is a `FormatException` thrown across a callback; `Window.Decode`'s bare `catch { }` (`code/Window.cs:150`, older) swallows it, while `Draw` catches it (`:134`). With a picture read that answers an error, both paths see one answer.
- **Scale:** one new small class (about 30 lines, the two Skia calls move in unchanged) and the call sites collapse; not a rename. Only the Windows host runs `Window`, so the coder cannot run it here: the picture type's tests carry the proof.

### F2. TIFF in `Sniff` changes `FromBytes` and no test names it (`image/this.Parse.cs:42-44`)
`Sniff` is shared. `ValidateKind` was ImageSharp's (which knew TIFF); `FromBytes` used `Sniff` and declined TIFF bytes. Adding the two magics gives `ValidateKind` its old answer, and also makes `FromBytes`/`Create(byte[])` name TIFF bytes `image<tif>` where they were declined: a behaviour change on a branch that intends none. **Deletion test:** the three lines can go and no test fails (`git grep` over `PLang.Tests` finds no TIFF byte or `tif` sniff). Either the change is wanted and a test pins it (II*\0 and MM\0* each to `tif` through `FromBytes` and through a strict `image/tif`), or the lines go and TIFF stays declined. The coder's call; the test is the coder's either way.

### F3. Three `using`s left in `crypto/code/Default.cs:1-3`
The branch removed `Nethereum.Util` from this file and left `System.Security.Cryptography`, `System.Text` and `System.Text.Json`, none of which the file uses (no `Aes`, `Encoding`, `Json*`, `SHA*` in it). Delete them.

## Judged, not blocking
- **`ValidateKind` (`image/this.cs:263-276`):** `object value` and the `(bool ok, string? actualKind)` tuple are the signature of `IKindValidatable` (`app.data`, pinned by the surface golden); this file cannot narrow them without a proposal to the architect and Ingi. Inside it: the `value switch` is three `is X` checks; `@this img => img.Bytes` and `_ => Bytes` make the probe read its own bytes when handed a non-bytes value, which is a (false, null) either way; the tuple's `null` actual kind means "the bytes name no image" and a name means "a different one", a second answer in a name. A smaller form without touching the interface: `value switch { byte[] b => b, item i => i.RawBytes, _ => null }`, so the default arm answers (false, null) without reading the probe. Older than the branch (since the `value` switch was written), reported as older.
- **Return race fix (`Schema.cs:62-79`):** correct (write `_return`, `Volatile.Write` the flag; reader `Volatile.Read` the flag, then `_return`); not testable deterministically, so no test is the right answer. Two fields kept in step by an order is what a `Lazy<type?>` is for (one field, no order to reason about); a note, not a finding.
- **Keccak (`keccak256/this.cs`):** correct. BouncyCastle's `KeccakDigest(256)` is the pre-FIPS padding (same as Nethereum's), takes bytes as they come, `DoFinal` resets. The `abc` vector in `HashActionTests` is the published one. The same file now matches `sha256/this.cs`'s shape (`BlockUpdate` as `AppendData`).
- **Fluid.cs (reshape, accepted):** one `Key(name)`, one `_include` map, errors answered whole (`AppException(exists.Error!)`), typed catches. One edge: `Text(key)` (`:501`) falls back to the name with `.liquid` cut, so `{% include 'x.liquid' %}` with no `x.liquid` reads a file named `x` if there is one. Intended for `include 'x'`; for `'x.liquid'` it is a second spelling for the same include. Older and small; say so in the method's comment or only fall back when the template wrote no extension.
- **Window / view Skia code:** the view's encode (`view/this.cs:66-69`) and the window's decode (`window/this.cs:156-161`) dispose every Skia object (`using`); `Unpremul` on both sides, which `PngTests` pins.
- **Csproj and targets:** `native.targets` drops `.pdb` from `NativeCopyLocalItems` and `RuntimeTargetsCopyLocalItems`; `PLangConsole.csproj` pins the machine's RID for a non-Release build with `AppendRuntimeIdentifierToOutputPath=false`, so `PlangConsole/bin/Debug/net10.0/plang` is unchanged; a publish for another RID works (below). `Directory.Build.props` imports `native.targets` once for every test project.

## Older, in the touched files (rule 66; marked older, none introduced here)
- `ui/code/Fluid.cs`: `IsPath` (:397) and `Folder(Render)` (:412) are instance helpers that use neither `this` nor state (stray helpers; `action.IsPath`/`action.Folder`); the `"app"`/`"context"` ambient keys are glued strings at :157-158 and read at :44 and :432-433 with casts; `isFile` null vs false sentinel (:66-74); `new Included().In(next)` (:486) is a throwaway made to read one list.
- `goal/step/action/this.cs`: `IsCondition`/`IsBranch`/`Link` (:116-132) are five string compares on module and action names where the condition clause could say its own link; `Level(int depth)` (:157) switches on an int; `Instance` (:352) and `Bind` (:385) return tuples of an object and an error where the rest of the file answers `Data`; `new bool Cacheable` hides the item base's; `Synthetic { get; set; }` is a late stamp.
- `crypto/code/Default.cs`: `Verify` reads `action.Hash.Value()` twice (:59, :73) and catches `FormatException` (:73-74) where `hash.FromBase64` could answer an error.
- `image/this.cs`: dead `cref="BytesAsync"` (:51-52, :69, :236), an orphan `<summary>` (:102-106), `Shape => "string"` (:54); `Parse.cs:41` names any bytes starting `BM` a bmp (two bytes of magic).
- `Tools/obp-longmethods.py:11-12`: the docstring still says the exact home is an H4 pass in `tools/ObpScan`, which is gone (the coder said the usage line was updated). `Documentation/Runtime2/data-spec.md:461` still describes the Newtonsoft `JToken` unwrap, which is deleted (docs).

## 2. The tests added: red without their change?
Announced mutations (production edited, run, reverted; `git status` clean after):

| Test | Mutation | Result |
|---|---|---|
| `RenderTests.Render_Include_NamedWithAndWithoutItsExtension_RendersBoth` | `Fluid.cs` back to 248f5cb8f (before the one-key reshape) | **red** (the only failure of 42) |
| `HashActionTests.Keccak256_OfNothing_…`, `Keccak256_OfAbc_…`, `APath_HashesItsFile_…` | `KeccakDigest(256)` to FIPS `Sha3Digest(256)` | **red** (3 of 22) |
| `PngTests.TheViewsPicture_ReadByTheWindow_IsThePixelsItShows` | `Unpremul` to `Premul` in `Pixels` | **red** |
| `PngTests.BytesThatAreNoImage_AreRefusedAsAFrame` | not mutated | pins the `FormatException` of F1 |

Two notes: `PngTests` waits for the view's thread by polling 100 × 20 ms (`PngTests.cs:41-46`), 2 s at most, which a loaded machine can miss (it was 2.4 s red, 0.5 s green here); and no test pours the keccak in more than one chunk (the change's point is "as the bytes come"), only the known single-chunk vectors. Neither blocks.

## 3. The bench (tip 1435afbf8, same production code as 63a299efb), against fast-apps' last row (review7-tip)
| | fast-apps | packages | |
|---|---|---|---|
| Sub / root birth | 0.14 ms 110 KB / 17.90 ms 2358 KB | 0.14 ms 109 KB / 17.07 ms 2358 KB | in band |
| Sub / root request | 1.82 ms 540 KB / 16.68 ms 3821 KB | 1.85 ms 539 KB / 17.02 ms 3821 KB | in band |
| Held, sub / root (MB) | 1.6→2.1→2.1→2.1→1.9 / 0.1→1.9→1.9→1.9→1.1 | identical | flat |
| `plang --test` | 5.3 s, 298 pass 1 fail | 5.4 s, 298 pass 1 fail | same |
| C# sweep | 40.5 s, red Wire | 39.7 s, red Wire (six, same names) | in band |
| Build, clean | 75.7 s (a neighbour); quiet 33.8–53.8 | **30.9 s** | under the band's low end |
| Build, incremental | 8.6 s | 8.3 s | in band |

Load 2.73 at the highest read; the bench had no OpenAI key (see 4), so its own sweep showed `Modules` red with 80 `MissingLlmKey` failures; rerun with a dummy key the sweep is green except `Wire`'s six. The row in `record.md` says so.

**What moved, and why:** only the build and the bins; the runtime path does not move (birth, requests and held memory are unchanged to the byte, as they must be for a bump with no behaviour change).
- **Clean build 30.9 s** (parent measured here 77 s at start load 4.7, a neighbour running, so read it as the band, not as a ratio). Plausible cause, not isolated: a clean build copies each of the six test projects' and the console's native files, and the dev builds now copy only the machine's runtime assets and no native pdbs (SkiaSharp's are about 95 MB each). I did not run a one-change-at-a-time build to separate this from the quiet machine.
- **PlangConsole Debug bin 50 → 29 MB** (parent 50 measured after a Clean; the coder's 67 → 28 is the same direction). A test bin 41 → 50 MB (+9: the Skia native, the machine's RID only). **Publish** linux-x64 27 MB, win-x64 29 MB, both with no native pdb (2 pdbs, the managed plang's); the publish for another RID builds with no NETSDK1047.
- No new package has a high advisory (the architect's table; I did not rerun `--vulnerable`).

## 4. Per commit: `plang --test` without `/module/llm`; the llm tests once
- **15 commits** (ab9459746 … 63a299efb), each built with `./dev.sh build` (62–150 s) and run with `--test.setting.exclude=["llm"]` and **`OPENAI_API_KEY` unset**: every one is **303 total, 292 pass, 1 fail, 3 stale, 7 skipped**, the one fail `BareLiteralJudgedByForm` (the parent's), the 7 skipped the llm-tagged tests. No commit differs from another.
- **The llm tests, once, at the tip with the key:** 303 total, 298 pass, 1 fail (the same), 3 stale, 1 skipped; the six `/module/llm` goals passed (2.1–2.7 s each, about one to two OpenAI calls each, around ten calls in all).
- **The C# suites do not call OpenAI:** the 80 `Query_*` tests in `Modules` use a counting HTTP handler (`_handler.CallCount`) and fail only with `MissingLlmKey` when no key string is present; with a dummy key they pass, so a live call would be a 401 and no spend. (Answers the architect's spend question: bench and sweep runs cost nothing; the six llm goals in `plang --test` are the only live calls.)

## 5. ObpScan (0e8097701, d8190936e, 59ff8bbe4; outside the build)
Pass. `Member.Shape` answers three flags from the symbol (RAW-COLLECTION: public, verb-named, return unwrapped from `Task`/`ValueTask` is an array or a generic `System.Collections.*`; GET-TWIN: `GetX` beside `X`; MUTABLE-COLLECTION: public property or field typed `List`/`Dictionary`/`HashSet`/`SortedDictionary`/`Queue`/`Stack`). `--tree PLang/app` ran here in 16 s and printed what the coder reported: 16 members in 846 types, 8 RAW-COLLECTION, 0 GET-TWIN, 8 MUTABLE-COLLECTION; none is in a file this branch touched (the list names `variable/list`, `error`, `llm message`, `Reserved`, `Core`, `filesystem`, `IClass`, `Entry`, `actor/context`). The Roslyn 5.9.0 bump and `Microsoft.Build.Framework` with `ExcludeAssets="runtime"` build and run. One leftover: `Tools/obp-longmethods.py:11-12` still names `tools/ObpScan`.

## Reply
CHECK: FAIL (F1 the window's pixels, F2 the TIFF sniff without a test, F3 three unused usings); the coder is next. Everything measured is in band or better.
