# packages — the tester's line pass (tip 235dc6087, parent runtime2 55302f8a5)

**Verdict: PASS, one minor finding.**

## Runs
- `./dev.sh test --force`: Modules 1,348 / Types 968 / Data 877 / Generator 191 / Runtime 1,357 green; Wire 601 with the same
  six reds as the parent (names in `.bot/packages/coder/v1/baseline-tests.md`). No new red name.
- `plang --test '--test.setting.exclude=["llm"]'`: 303 total, 292 pass, 1 fail (`BareLiteralJudgedByForm`), 3 stale, 7 skipped.
  The parent's 292 + 1.
- `plang --test` once with llm: 298 pass, 1 fail (same), 3 stale, 1 skipped. Same as the parent's baseline. `git status` clean after.

## Removed tests
None in the build. Test-project diff has no removed `[Test]`. Assertions changed: `HashActionTests` (Nethereum reference replaced by
published keccak vectors), `ShownTests` (`Pixels` → `Bitmap.Pixels`, same checks). `PlangTests/` (v1, `PLangTests.csproj`) was in
`PLang.sln` but is outside the dev.sh build/test projects; `PlangWindowForms/` likewise. NSubstitute and coverlet references removed from the test csprojs.

## Mutations (each reverted; tree clean)
| Change | Mutation | Red |
|---|---|---|
| `Bitmap.cs:27` straight alpha | decode as `Premul` | `PngTests.TheViewsPicture_ReadByTheWindow_IsThePixelsItShows` |
| `Bitmap.cs:36` BadFrame | key `BadFrames` | `BytesThatAreNoImage_AreRefusedAsAFrame`, `APngWhosePixelsAreJunk_IsRefusedAsAFrame` |
| `Bitmap.cs:29-33` decoder exception becomes the cause | rethrow | `APngWhosePixelsAreJunk_IsRefusedAsAFrame` |
| keccak `keccak256/this.cs:11` | `Sha3Digest` (FIPS padding) | `Keccak256_OfNothing_…`, `Keccak256_OfAbc_…`, + the file-stream vector (3) |
| TIFF sniff `image/this.Parse.cs:43-44` | `"tif"`→`"tiff"` | `ValidateKind_TiffBytesMatchingTif_OkTrue` ×2 (`FromBytes` stays green: the mime is the same) |
| TIFF sniff | `MM` branch off | `FromBytes_TiffMagicBytes_PicksImageTiff(MM)`, `ValidateKind_…(MM)` |
| include key `Fluid.cs:477` | key = the name as written | `Render_Include_MissingPartial_ReturnsError` only; the new test stays green |
| include dedupe `Fluid.cs:489` | removed | nothing red |
| `Fluid.cs` whole file at parent | parent version | all include tests red, new one with them |

## Finding
1. (minor) `RenderTests.cs:351` `Render_Include_NamedWithAndWithoutItsExtension_RendersBoth` does not pin what its comment says
   ("the one include, read once"). Fluid asks for `partial.liquid` for both spellings, so the key (`Fluid.cs:477`) and the
   dedupe (`Fluid.cs:489`) can be broken and it stays green. It is red only against the whole parent `Fluid.cs`, which fails
   every include under Fluid 2.40. Suggest: count the reads (a counting file or a goal-variable the include's `Text` bumps),
   or drop "read once" from the comment.
