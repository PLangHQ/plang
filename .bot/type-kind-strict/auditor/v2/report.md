# auditor v2 — type-kind-strict

**HEAD:** `932564d6e` (coder v14 — `Data.Load()` at the serialize chokepoint).
**Verdict:** **PASS** → next: **Ingi (branch ready to merge)**.

## What changed since v1

Single commit `932564d6e` resolving F1. Production source:
- `PLang/app/data/ILoadable.cs` (new — marker for I/O-backed values, `LoadAsync()`)
- `PLang/app/data/this.Load.cs` (new — graph walk awaiting `LoadAsync()` on each `ILoadable`; cycle guard + depth cap mirror `Normalize`)
- `PLang/app/type/image/this.cs` — `image` implements `ILoadable`; `LoadAsync() => BytesAsync()` (loads + caches + runs strict check)
- `PLang/app/channel/serializer/plang/this.cs:141` and `Json.cs:77` — `await data.Load()` first line inside the existing `try` of `SerializeAsync`
- `PLang.Tests/.../ReferenceFundamentalTests/LoadSeamTests.cs` (+6 tests)

## Re-walk of F1's seam

The v1 finding was: `image.BytesAsync()` has zero production callers; all consumers use sync `value.Bytes` which returns `Array.Empty<byte>()` for a path-backed image, so strict enforcement and rendered bytes are both unreachable.

v2 trace:

1. **Chokepoint coverage is complete.** `grep -l "SerializeAsync(Stream"` returns three impls — `plang/this.cs`, `Json.cs`, `Text.cs`. `Text.cs:28` short-circuits non-primitives to `_jsonFallback.SerializeAsync` (`Json.cs`), so any image-bearing graph leaves via either `plang` or `Json` — both call `data.Load()` before the STJ wall. The `ContextLessFallback` (`data/this.Transport.cs:118,187`) is `plang.@this`, so transport egress passes through the same gate. No bypass found.
2. **`Load()` runs above the sync converter wall and reaches every shape `Normalize` reaches.** `this.Load.cs:44-101` walks nested `Data`, dicts, lists with reference-equality cycle guard and the same `MaxNormalizeDepth` cap. Reference fundamentals are hit directly (`if (value is ILoadable)` line 58); reflection on arbitrary objects is deliberately omitted (PLang value model puts reference fundamentals in the value slot or in dict/list, not behind POCO properties).
3. **Strict mismatch is surfaced before any bytes write.** `Load()` catches `StrictKindMismatchException` (line 38) and returns a `FromError` with key `StrictKindMismatch`; both serializer call sites return that error verbatim without entering `JsonSerializer.SerializeAsync` (`plang/this.cs:142`, `Json.cs:78` → `return loadError`). Zero bytes torn. The `LoadSeam_SerializeStrictMismatch_FailsCleanlyWithZeroBytes` test pins this.
4. **Read failures from path-backed loads propagate cleanly.** `image.BytesAsync` throws `IOException` (`image/this.cs:122`) when `Path.ReadBytes()` fails. Not caught by `Load()`; it surfaces to the serializer's existing `catch (… or IOException …)` (`plang/this.cs:146`, `Json.cs:89`), returning `PlangSerializeError`/`SerializeError`. No silent swallowing.
5. **Bytes-backed and lazy-free graphs are free.** `Load()` returns `null` after a pure walk; `LoadAsync` short-circuits on `_bytes != null` (`image/this.cs:118`). The new `LoadSeam_NoOpForBytesBackedImage` and `LoadSeam_NoOpForScalarGraph` tests pin this.
6. **Mutation evidence holds.** I re-read both new serialize tests: they assert the OBSERVABLE (non-empty base64 in the rendered output; `StrictKindMismatch` error key plus zero bytes on the destination stream). Neutering `await data.Load()` would flip them as the coder claims.

The cross-file gap is closed. The seam is no longer "BytesAsync exists but nothing calls it" — `BytesAsync` has exactly one production caller (`image.LoadAsync`), and `LoadAsync` is invoked from the un-bypassable chokepoint.

## Verification

- Clean rebuild from scratch (`rm -rf bin obj` then `dotnet build PlangConsole`): **0 errors**, no PLNG001/002 regressions.
- **C# suite: 4031/4031** (was 4025; +6 from `LoadSeamTests`).
- **PLang goal suite: 273/273** from `cd Tests && plang --test` on the freshly-built binary.

## Lingering items (deliberately deferred — not blocking)

Coder v14's report flags `Width`/`Height` and `ValidateKind`'s `_ => Bytes` fallback as other sync byte readers. I confirmed: they sit off the serialize wall and are reached only from already-async action handlers (e.g. an action that asks for `%img.Width%` runs through an async handler that could `await BytesAsync()` first). They're a smaller sweep and don't gate the F1 contract — the headline flow (`set ... as image/<kind> strict` → output/wire) is now correct. Tracking the sweep is on coder/architect to decide; auditor doesn't block on it.

## Files
- `.bot/type-kind-strict/auditor/v2/report.md`
- `.bot/type-kind-strict/auditor/v2/verdict.json`
