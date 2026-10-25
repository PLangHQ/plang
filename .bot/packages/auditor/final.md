# packages — final audit (auditor)

**Verdict: PASS.** Tip 6a4f56f83 (parent runtime2 55302f8a5). Next: docs. Nothing blocks the merge. The items below
are docs-shaped (D1–D3) or belong to later branches (N1–N4).

Checked at the tip: `./dev.sh build` clean (63 s). Sweep: Modules 1,361, Types 968, Data 877, Generator 191, Runtime 1,357
green; Wire 602 with 6 red, the same six names as the parent (the snapshot/resume names). `plang --test`
(`--test.setting.exclude=["llm"]`, no OpenAI): 303 total, 292 pass, 1 fail (BareLiteralJudgedByForm), 3 stale, 7 skipped,
the same as the parent. `dotnet list package --vulnerable --include-transitive`: none in any solution project. `--outdated`:
only `Microsoft.CodeAnalysis.CSharp` 4.13.0 in the generator and its test project (kept on purpose, decision 3).

## 1. Decisions 1–19 against the code

| # | says | found |
|---|---|---|
| 1,2 | upgrade order, rules | PLang.csproj: Fluid 2.40.0, Microsoft three 10.0.13, NSec 26.4.0, BouncyCastle 2.7.0, Skia 4.153.1 (+Linux NoDependencies). Tests: TUnit 1.76.0, coverlet 10.1.0 (`PLang.Tests/Directory.Build.props:40-41`, `Shared.csproj:15-16`). |
| 3 | Newtonsoft and Nethereum go; Roslyn stays | `git grep Nethereum\|Newtonsoft` in code: none (only tests that pin the absence). `[Newtonsoft.Json.JsonProperty]` gone (`goal/step/action/this.cs:28`). Keccak is BouncyCastle `KeccakDigest(256)`, bytes as they come (`keccak256/this.cs:11-18`), pinned by published vectors (HashActionTests, Keccak256VectorTests). |
| 4 | ImageSharp stops | no `SixLabors` anywhere outside `.bot/`. |
| 5,6,7 | Skia replaces it; trims | `native.targets` drops native pdbs, imported by PlangConsole and the test props. The `<RuntimeIdentifier>` block in PLangConsole.csproj is for non-Release only. `image/this.cs:274` asks `Sniff` and no library; TIFF magics at `this.Parse.cs:42-44`. |
| 8 | NSubstitute goes | no `NSubstitute` reference left. |
| 9 | PlangTests/, PlangWindowForms/ go | both gone, removed from PLang.sln. **Two live files still name them: D2.** |
| 11,12 | one ObpScan | `tools/ObpScan` gone; `Tools/ObpScan` has the Shape column; `Tools/obp-longmethods.py` moved. |
| 13,14,15 | F1–F4 | F1: `Bitmap` owns a picture (`screen/code/Bitmap.cs`), `Shown.Bitmap`, the window's `bitmap`. F2: TIFF pinned (ImageParseTests, ImageValidateKindTests). F3: usings out of `crypto/code/Default.cs`. F4: `Bitmap(byte[], max)` is an AppException carrying BadFrame 400, Skia's exception its cause (`Bitmap.cs:36-41`). |
| 16,18,19 | S1, `screen.setting.max` | `screen/setting/this.cs:15`: `[Out, Store, Default("128 MiB")] size Max`. `Bitmap.cs:21-23`: `(long)w*h > max/4` before any decode, no overflow, `Bytes` inside the guard. Message names `screen.setting.max` and `--screen.setting.max=1GiB`. FrameBoundTests (8000×8000 at the default, BadFrame, message), PngTests (600×600 refused at 1 MiB, read at 2 MiB, junk IDAT at 16 GiB). |
| 17 | tester's minor | RenderTests' comment now claims only that both spellings render. |

The bound's arithmetic was read by hand: width and height are ints, so the product fits a long, and `/4` goes on the max side.
8000×8000 = 64 M px > 128 MiB/4 = 33.5 M px, refused. 7680×4320 = 33.2 M px, read: the 8K frame the decision names.

## 2. The locked surface

`git diff 55302f8a5..HEAD -- surface_golden.txt` is one line, `type app.module.screen.setting`, after the blank line (+2 lines
with it). It is a module-owned type, approved in 18 (Ingi). Nothing else on a locked surface moved: the Schema.cs change is
`action.@this.Return`, a private body (a Volatile-published type before its flag, decision 4; no signature change), and the
SurfaceTests change is the test's own diff helper (clamped counts, with a case red before: `SurfaceTests.cs:145-146,177-187`).
`ValidateKind` keeps its locked `(bool, string?)` shape.

## 3. Rule 35 and the boy scout rule

- No production member exists for a test: `Bitmap` and `Shown.Bitmap` are `internal`, used by production (the view's `Picture`,
  the window). The old test-only `internal Pixels` is gone; ShownTests and PngTests enter through production's door.
- `Bitmap` replaces the raw `(bgra,w,h)` tuple and the swallowed catch. Fluid.cs: one `Key`, one `_include` map, errors answered
  whole (`:437`, `:488-519`), the two typed catches (`:199-207`). `Shown`, `view`, `window`, `Parse`, keccak256, crypto
  `Default` (usings) and `Schema` are each touched narrowly and read whole by the codeanalyzer.
- Window.cs, the Windows-only native window, was touched only to hold a `Bitmap`. The boy scout rule can't run there (no Windows
  build here), which is the architect's call (14) and Ingi's.

## 4. Speed

`Tools/Bench/record.md` carries a packages row at 1435afbf8 (load 2.73): sub birth 0.14 ms / 109 KB, root birth 17.07 ms /
2,358 KB, sub request 1.85 ms / 539 KB, root request 17.02 ms / 3,821 KB, held memory flat; all inside the bands. I did not
re-run the bench: after that row the diff holds only the Bitmap bound, the test renames and the SurfaceTests clamp, none on the
birth or request path.

## 5. Findings

| id | sev | file:line | issue | disposition |
|---|---|---|---|---|
| D1 | minor | `docs/modules/crypto.md:56` | still says `keccak256` is "Nethereum Sha3Keccack (default)". It is BouncyCastle's `KeccakDigest(256)`. | docs |
| D2 | minor (cross-file) | `Publish/Publish.goal:20`, `Publish/Nuget.goal:9` | the release goals still `dotnet publish PlangWindowForms/PLangWindowForms.csproj` and `dotnet pack … ../PlangTests`, both deleted by decision 9. A release run fails at those steps. Nobody checked the `.goal` consumers of the deleted folders. Missed by: all reviewers (they grepped csproj/props/sln/scripts). | docs, or the architect with Ingi: delete the steps or point them at the new way. Not a build gate. |
| D3 | minor | `Documentation/Runtime2/data-spec.md:461` | describes the deleted Newtonsoft unwrap | docs (already on the list, 11/13) |
| D4 | — | `.bot/packages/claude-md-proposals.md` | two proposals are waiting | docs |
| N1 | minor, deferred | `PLang/app/module/screen/type/screen/window/code/Window.cs:236,305,320` | a message's own `w*h` is multiplied for QOI parts, in the Windows-only duplicate; its own QOI decoder and delta logic beside `code/Qoi.cs`, `Shown.cs` | with Ingi (11, 14); the bugs branch |
| N2 | minor, deferred | `Window.cs:110-116` and `:198-201` | `Offer` keeps the first decoder, so the first draw's `context` is captured for every later frame; and a `Patch` before the first `Offer` installs a no-op decoder so frames are never decoded (pre-existing, `decode = _ => null` before this branch). Windows-only, unreproduced here. | Windows item with Ingi |
| N3 | nit, deferred | `window/this.cs:18,117` | `_max` mirrors the setting and is read on `Draw` for patches too (the architect's own note, 21) | Windows item |
| N4 | nit | `Ui/Fluid.cs:75,86` | `template.Clr<string>() ?? ""` replaced `.ToString()`: a template that is not text now becomes an empty template. Not reachable through the `.pr` types today; I did not test it. | next touch of Fluid |

No critical or major finding. Reviews assessed: codeanalyzer (PASS 18/19) agrees; tester (PASS, six mutations) agrees;
security (PASS, S1 closed, probes by concept after 6e8e7b74f) agrees; the rename left the 11 probe tests green, now in
Modules (1,361 = 1,350 + 11, Runtime 1,357 = 1,368 − 11).
