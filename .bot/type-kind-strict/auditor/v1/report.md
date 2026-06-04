# auditor v1 — type-kind-strict

**HEAD:** `e847bf8ff` (post tester v13 PASS, security v1 PASS, codeanalyzer v3 PASS — merged with `lazy-deserialize`).
**Verdict:** **NEEDS WORK** → next: **coder**.

## Upstream coverage check
- codeanalyzer v3 ran on `f971f98e6`; tester v13 on `ced2a2517`; security v1 on `ced2a2517` (=HEAD-1). All commits between codeanalyzer v3 and HEAD are bot reports only (`git diff f971f98e6..HEAD --stat` → only `.bot/**`). Production code last touched by `d4fdd030c` (the `lazy-deserialize` merge). Coverage is complete.

## Cross-seam trace — what I verified

I started from codeanalyzer v3's load-bearing claim ("strict-kind × lazy passthrough is CLEAN") and walked each named line.

1. **`variable/set.cs:181-195` — strict runtime probe.** Reads `Value.Value`. Confirmed at `data/this.cs:182-184`: a raw-backed `Data` materializes on `.Value` read (`_value = Materialize()`), and `RawUntouched` (line 272) is `_raw != null && _value == null && _valueFactory == null` — so the probe correctly flips `RawUntouched` to false for `IKindValidatable` types. Gate `typeEntity.Strict && Kind != null` is the only path that reads `Value.Value` early; the non-strict raw-backed flow keeps the passthrough at 203-209 valid. Traced clean.
2. **`variable/set.cs:264-274` — strict imprint at binding-mint.** `enforcer.RequireStrictKind(kind)` then `CheckStrictKind()`. For a path-backed image, `_bytes == null`, so `CheckStrictKind()` returns `null` and validation defers (per design). Traced clean **structurally**, but see F1.
3. **`variable/list/this.cs:278, 311` — set-path MaterializeFailed.** Both guards surface `parent.Error` as `FromError` rather than swallowing as `NotFound`. Mirrors the navigation-side `this.Navigation.cs:254, 279`. Traced clean.

## Finding

### F1 — Path-backed strict-kind enforcement is unreachable from any production caller (Major)

The architect's stage-9 contract (`architect/stage-9-lazy-reference-handles.md`, summarized in `summary.md` line 7): "content loads from the path on first access"; "Strict kind for a reference fundamental rides WITH the value to its load seam (image.BytesAsync throws on mismatch)." This is the **load seam** the codeanalyzer v3 trace and tester v13 both lean on.

The load seam is `app/type/image/this.cs:116-128 BytesAsync()` — the **only** entry point that throws `StrictKindMismatchException`. Verified with `grep -rn "BytesAsync()" PLang --include="*.cs"`: **zero production callers** outside the method's own definition. The consumers that *would* read image content all use the sync `value.Bytes` property (line 53), which returns `_bytes ?? Array.Empty<byte>()`:

- `app/type/image/serializer/Default.cs:13` — base64 renderer (used by `json` + `plang` channels): `writer.String(Convert.ToBase64String(value.Bytes))`.
- `app/type/image/serializer/text.cs:21` — bare-label fallback: `value.Bytes.Length`.
- `app/type/image/serializer/protobuf.cs:13` — `writer.Bytes(value.Bytes)`.
- `app/type/image/this.cs:72, 75` — `Width`/`Height` via `ProbeDimensions()` (line 199 reads `Bytes`).
- `app/type/image/this.cs:149` — `AsBooleanAsync()` short-circuits on `_bytes != null` before falling to `Path.ExistsAsync()` (so truthiness can dodge the load too).

Concrete consequence for the user-visible flow promised by stage 9:

```
- set %img% = "wrong.png" as image/gif strict   # mint path-backed image, _requiredKind="gif"
- write out %img%                                # render via channel.write → Default.Write
```

- `Default.Write` reads `value.Bytes` (sync) → returns `Array.Empty<byte>()` because `_bytes == null` for a path-backed image. Output is `""` (base64 of empty).
- `BytesAsync()` never fires → strict check never runs → no `StrictKindMismatchException`.
- The actor's strict declaration is silently ignored.

Same shape regardless of mismatch: even a *valid* path-backed image renders as empty bytes, because no consumer pulls bytes through the async load. The stage-9 "lazy reference handles" feature is structurally present (handle minted with `.Path` set, strict imprinted) but the *first-access load* is never triggered from any real flow.

**Why upstream bots missed it:**
- Codeanalyzer v3 traced `variable.set.cs:264 → enforcer.RequireStrictKind` and stopped at "enforcement defers to `BytesAsync`." It did not verify any production code calls `BytesAsync`.
- Tester v13 reports `PLang 273/273`; the goal-level tests `Tests/TypeKindStrict/SetAsImageGifStrict*.test.goal` stop at `assert %img.Type.Kind% equals "gif"` and explicitly punt to a unit test (`SetAsImageGifStrictMismatch.test.goal:5` comments: "throw-at-load is covered by the C# LazyPathHandleTests.BytesAsync_StrictKindMismatch_ThrowsAtLoad"). That C# test (`PLang.Tests/App/TypeKindStrict/ReferenceFundamentalTests/LazyPathHandleTests.cs:87-100`) constructs `new image.@this(path)` directly and calls `await img.BytesAsync()` — it tests the method, not the seam. There is no test where binding via `variable.set` is followed by a *consumer-driven* read.
- Security v1 audited the strict×lazy seam under the assumption that enforcement fires; the unreachability inverts the threat model from "strict throws too eagerly" to "strict never runs, declared kind is advisory" — a different concern not in scope of that audit.

**Severity rationale:** The stage-9 promise is the headline feature this branch ships. A path-backed image declared `as image/<kind> strict` silently passes through any output flow with empty bytes and no enforcement — the declared safety property is unmet, and the rendered output is also wrong (empty). This isn't an edge case; it's the primary user-visible flow for the feature.

**Not tracked anywhere as deferred work.** `Documentation/Runtime2/todos.md` has nothing on image-consumer migration to `BytesAsync`. Architect summary mentions only `Bytes/Width/Height` "sync getters cannot do the load" as a *design* note resolved by adding `BytesAsync`; consumer migration is implied but never carved as a stage.

**Possible fixes** (coder picks):
1. **Wire the leaf serializers to async.** `IWriter` already operates across async boundaries (`channel/serializer/Text.cs`, `app/data/Wire.cs` Read is async). Add an `image` write-path that awaits `BytesAsync()` before handing bytes to the writer. Forces an audit of every sync `.Bytes` reader to either call `BytesAsync` first or surface a "not materialized" error.
2. **Eagerly materialize at navigation.** `Data.ForceMaterialize` (line 314) is the existing courier-safe surface; teach it to load path-backed `IStrictKindEnforcer` content (and surface failure as `MaterializeFailed` / `StrictKindMismatch` on `Data.Error`, mirroring the lazy-deserialize seam). Then sync `Bytes` works after the first navigation. Loses some of the laziness Ingi explicitly wanted at `set` time, but keeps it lazy across navigation seams.
3. **Document and defer**, with a goal-level test that pins the *current* behavior (empty bytes, no strict throw) and a tracked todo. Honest about scope; locks in the gap so it can't regress further.

Option 1 matches the architect's "async load seam" framing most directly; option 2 is the smallest blast radius if Ingi wants this resolved without changing the writer surface.

## What I did NOT find

- The `variable/set.cs` RawUntouched passthrough is safe across the strict×lazy boundary as codeanalyzer v3 claimed — verified line by line.
- Set-path `MaterializeFailed` surfacing matches the navigation seam — auditor v2's prior fix (`602c4f8ff`) carries through cleanly.
- No silent error swallowing in the seams I walked; the gap is *absence of a call site*, not a courier eating an error.

## Files touched
- `.bot/type-kind-strict/auditor/v1/report.md` (this file)
- `.bot/type-kind-strict/auditor/v1/verdict.json`
