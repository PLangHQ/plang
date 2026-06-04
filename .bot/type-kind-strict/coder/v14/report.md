# coder v14 report — resolved auditor v1 F1 (lazy reference-fundamental never loads)

**Responds to:** auditor v1 (NEEDS WORK, Major F1). Design call by Ingi in-session
(dedicated error key; implement directly). Plan: `coder/v14/plan.md`.

## What was broken (confirmed the auditor — one root, two symptoms)

`image.Convert` mints a lazy path-backed handle for `set %img% = "x.png" as image…`;
the only loader+strict-check (`image.BytesAsync`) had **zero production callers**.
Every consumer read the sync `.Bytes` getter → `Array.Empty<byte>()`. So a
path-backed image (a) rendered as **empty bytes** in every channel and (b) never
enforced `strict`. Both are the same root: nothing triggered the async load.

Why "make consumers async" was the wrong fix: the byte read happens inside
`Wire.Write`, which is `JsonConverter<@this>.Write` — sync by the System.Text.Json
contract. Nothing below `JsonSerializer.SerializeAsync` can `await`. The load
must happen **above the STJ wall**.

## The fix — `Data.Load()`: one async materialization pass at the serialize chokepoint

- **`app/data/ILoadable.cs`** — marker for a value with lazy I/O-backed content
  (`Task LoadAsync()`). Distinct from `IStrictKindEnforcer`: a lazy handle needs
  loading whether or not it is strict.
- **`image.LoadAsync()`** — `=> BytesAsync()` (loads, caches, runs the strict check).
- **`app/data/this.Load.cs`** — `Data.Load()` walks the same value shapes as
  `Normalize` (nested Data / dict / list, cycle guard + depth cap), awaiting
  `LoadAsync()` on each `ILoadable`. Returns `null` on success, or an error Data
  (key **`StrictKindMismatch`**) on a strict mismatch — surfaced before any bytes
  reach the stream. Read failures propagate to the serializer's existing catch.
  Idempotent and cheap when nothing is lazy.
- **Call site** — first line inside the `try` of each `ISerializer.SerializeAsync`
  (`plang/this.cs`, `Json.cs`). `Text.cs` delegates non-primitives to the Json
  fallback → covered. The leaf impl is the un-bypassable chokepoint (channel
  output + wire egress both pass through it).

Serialization engine untouched: no change to `Normalize`, the renderer delegate,
the STJ converters, or the sync `.Bytes` getter.

## Tests — `PLang.Tests/.../ReferenceFundamentalTests/LoadSeamTests.cs` (6)

Drive the consumer-facing flow the goal tests punt on:
- `Load` materializes a path-backed image (sync `.Bytes` then real).
- `Load` walks a nested image inside a dictionary.
- `Load` on a strict mismatch returns the dedicated `StrictKindMismatch` error (no throw).
- `Load` is a no-op for bytes-backed images / scalar graphs.
- **Serialize** a path-backed image → output contains real base64 (`iVBOR`), not empty.
- **Serialize** a strict mismatch → fails cleanly, key `StrictKindMismatch`, **0 bytes written**.

**Mutation-verified:** neutering `await data.Load()` in the plang serializer turns
the two serialize tests red (empty output / no strict throw); reverted, source clean.

## Verification (clean rebuild)

- `dotnet build PlangConsole` → **0 errors** (no PLNG001/PLNG002).
- C# suite: **4031 / 0** (was 4025; +6 new).
- PLang goal suite (`cd Tests && plang --test`): **273 / 273 pass, 0 fail**.

## Follow-up (flagged, not folded in)

`Width`/`Height` and `ValidateKind`'s `_ => Bytes` fallback are other sync byte
readers, but they sit off the serialize wall and are reached from already-async
action handlers — they can `await BytesAsync()` at their own call sites. Separate
small sweep; not part of this fix.
