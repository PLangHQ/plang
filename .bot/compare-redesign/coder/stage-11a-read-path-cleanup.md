# Stage 11A — read-path cleanup (decode-once; container parsing on the types; Judge STAYS)

**Status:** ready to implement. Self-contained pickup doc for a fresh context.
**Branch:** `compare-redesign`. **Owner:** coder.
**Read first, in order:** this doc → `deserialize-flow-design.md` (the agreed design + the architect review note at the top) → `architect/stage-11-lazy-read-and-containers.md`. Also skim `list-dict-raw-slot-model.md` (the container raw-slot model already landed) and `handoff-value-model-2026-06-17.md` (the binary/kind work that landed *after* the design doc was written — this is why you MUST re-ground, see Step 0).

---

## The goal in one paragraph

Simplify the wire **read** path so a value is decoded **once** and handed to the type that owns it, deleting the throwaway-tree machinery the path accreted. The type already owns its read via the **reader registry** (`type.Read(object raw, kind, ctx)`); the work is to stop building intermediate DOMs / re-stringifying around it, and to move container (`list`/`dict`) element-parsing **onto those types** (interlocking with the raw-slot container model already landed). **`Judge` STAYS for now** (Ingi's call) — its kind/strict reconciliation is separable from this cleanup and runs fine on the single decoded value.

## What this is NOT

- **Not building `IReader`.** The architect rejected it: `Utf8JsonReader` is a `ref struct` (stack-only), so it can't be a field or cross an interface — a format-agnostic token-streaming `IReader` mirror of `IWriter` is impossible without hand-rolling a non-stack reader, just to skip one cheap decode. The read side is intentionally **not** symmetric to `Write(IWriter)`. Do not build `IReader`.
- **Not removing `Judge`.** Out of scope. Leave `type.Judge` and its callers alone.

## How a value is created on read (the read "mirror")

`Write(IWriter)` is push/token-stream out. There is **no** token-stream in. A value is born from an **already-decoded plain value** through one of:

1. **`type.Read` via the reader registry** — `Context.App.Type.Readers.Of(typeName, kind)` returns a reader that takes a decoded `string`/`long`/`List`/`Dictionary` (never JSON) and returns the item. *This is the read mirror* — post-decode granularity, format-agnostic. (See `item/source.cs` `Value()` for the canonical use.)
2. **`type.Convert(value)`** — family hook (json-string→dict, primitive coercion).
3. **`Data.Lift(clrValue)`** — the CLR→plang inbound boundary (see `clr-plang-boundary.md`).
4. **`item.source`** — the lazy form: holds raw bytes + a `{type,kind}` stamp, materializes via #1 on `Value()`. This is how **verbatim passthrough / lazy** is achieved (a never-touched value stays raw).

The decode step *between bytes and #1* is where the historical waste lived.

## Current state of `Wire.ReadBody` (`PLang/app/data/Wire.cs`)

Re-verify these line numbers — they drift.

- **Eager value slot (≈ line 348-349):** already decode-**once** — `JsonDocument.ParseValue(ref reader)` then `item.serializer.json.Parse(RootElement)` → a born value. This is the accepted "decode once into a plain value, hand to the reader" shape. (Building a `JsonDocument` here, not streaming off `Utf8JsonReader`, is the accepted cost of *not* having `IReader`.)
- **Lazy/deferred value slot (≈ line 332-339, `deferredRaw`):** for `IsDeferrableShape` types (`object`/`item`/`table` **with a kind**) it does `JsonDocument.ParseValue` → **`GetRawText()` (re-stringify)** → `FromRaw(deferredRaw, …)` so the value stays unparsed for verbatim passthrough. The `GetRawText` re-stringify is the wart.
- **Machinery to retire:** `_readDepth`/`MaxReadDepth` (recursion guard, ≈ line 136/155/174), `deferredRaw` + `IsDeferrableShape` (≈ line 261/332/369), and any `LiftDataIfShaped`/`LiftArrayElements` still referenced (grep — some may already be gone after the binary/kind work).
- **Nested Data DOES ride a value slot for `@schema:data`** (≈ line 344-347 comment) — the design doc's "no nested Data ever" is now **false**; `@schema:data` is the one place a nested Data rides (signatures, snapshot section lists). Preserve that path.

## Step 0 — RE-GROUND before changing anything (mandatory)

The `deserialize-flow-design.md` was written **before** the binary/kind + raw-slot-container work landed (`handoff-value-model-2026-06-17.md`). Some of what it proposes to delete is **already done** (the eager decode-once), and some of what it proposes (delete `deferredRaw`) **conflicts with a real requirement** (verbatim passthrough / lazy needs an unparsed form). So:

1. Read the current `Wire.cs` end-to-end. Confirm which of `{deferredRaw, IsDeferrableShape, _readDepth, LiftDataIfShaped, LiftArrayElements}` still exist.
2. For each, decide: dead remnant (delete) vs load-bearing (lazy/verbatim — keep but de-wart). **The lazy path is a requirement, not waste** — Stage 3's "verbatim passthrough = the never-narrowed path." Do not delete laziness; remove only the *re-stringify* within it.
3. Write a 5-line findings note (what's already done, what's actually left) before editing. If the path turns out already-clean enough, say so and stop — don't manufacture work.

## The work (after Step 0 confirms it)

1. **Lazy slot without re-stringify.** Replace the `deferredRaw`/`GetRawText` capture so a deferred value rides as raw **without** round-tripping through a re-stringified string — ideally born straight into an `item.source` (or kept as the decoded value the source can re-encode on demand). The single subtlety of the whole task: keep verbatim passthrough working (read→write-out of an untouched value must reproduce the bytes) while not re-stringifying eagerly. Verify against the verbatim-passthrough / never-narrowed tests.
2. **Container element-parsing onto `list`/`dict`.** Move any array/object element-walk that still lives in `Wire` onto the container types (a `list`/`dict` parses its own elements, each recursing through the Data envelope). This dovetails with the raw-slot model (`list-dict-raw-slot-model.md`) — the container backs raw slots and types an element on read.
3. **Delete the dead machinery** confirmed in Step 0 (`_readDepth`, `IsDeferrableShape`, etc.) once nothing references it.
4. **Leave `Judge` in place.** It reconciles kind/strict on the single decoded value. Its removal is a *later* step, easier once the read path is clean.

## Caveats / invariants to hold

- **Verbatim passthrough** (read→write-out of an untouched value = byte-identical) must survive. This is the reason the lazy form exists.
- **`type` precedes `value` on the wire** — so `typeRef` is known at the value token. Keep that writer invariant; it's what lets the type own the parse.
- **`@schema:data` nested-Data** in a value slot is real (signatures, snapshot section lists). Don't "no nested Data" it away.
- **Judge stays.** Don't touch `type.Judge` or its callers.

## Files

- `PLang/app/data/Wire.cs` — `ReadBody` (the value loop), `IsDeferrableShape`, `_readDepth`, the writer side for the passthrough invariant.
- `PLang/app/type/item/source.cs` — the lazy form (`Value()` → reader registry); the lazy slot should land here.
- `PLang/app/type/list/this.cs`, `PLang/app/type/dict/this.cs` — container element parsing (raw-slot model already here).
- `PLang/app/type/this.cs` — `Build`/`Deserialize`/`Judge` (Judge stays; Deserialize may have a JsonElement-unwrap wart to simplify).
- Reader registry: `Context.App.Type.Readers` (the `type.Read(raw, kind, ctx)` surface).

## Verify

- `./dev.sh build` clean, then the per-suite sweep (see `CLAUDE.md` "Running plang Tests"); baseline below.
- Watch especially: `PLang.Tests/Wire/**` (serialization round-trips), `PLang.Tests/Data/App/LazyDeserialize/**`, the verbatim-passthrough / never-narrowed tests in `Stage3_*`, and the `binary/kind` decode tests (json→dict, csv→table, image, md→text, `.pr`→goal — listed in `handoff-value-model-2026-06-17.md`).
- Full-sweep diff against baseline; zero new failures is the bar. Every shared-code change → full sweep (this path is very shared).

## Baseline at handoff (HEAD on `compare-redesign` after the value-model + url/signing + clr-unskip work)

C# suite: **0 real failures.** Two HTTP tests (`Redirect_ToUnauthorizedHost`, `Post_405`) are **flaky under full-suite server contention** — they pass isolated; ignore unless they fail isolated.

Skipped (do NOT expect to unskip with this work): snapshot-redesign (3: `Snapshot_CapturesByReference`, `Snapshot_NullValuedVariable`, `ErrorsTrail`), archive-as-layer (8), pure-lazy source-gen (8). `ErrorsTrail` is the closest neighbor — it's the snapshot serializing `IError` via Data-normalization as an empty `[Out]` bag; if your read-path work happens to fix the round-trip, great, but it's filed under the snapshot redesign, not here.

## Related model context (memory / docs)

- `clr-plang-boundary.md` — the CLR⇄plang boundary (Lift inbound, .Clr outbound), and the everything-plang-internally endpoint (Stage 10, the architect is mapping that separately — not this work).
- `null_model_no_absent` (memory) — `absent` was collapsed into the null citizen; value-less = `IsNull`. Relevant because a lazily-unmaterialized/failed read surfaces as the null citizen now.
- `data_value_model` (memory) + `data-value-model.md` — the type-instance-IS-the-value contract this all serves.
