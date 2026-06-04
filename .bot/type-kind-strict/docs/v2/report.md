# docs v2 — type-kind-strict (PASS)

**HEAD:** `7b438a4e5`. **Verdict:** **PASS** → next: **Ingi** (branch ready to merge).
**Supersedes:** docs v1 (`35e44c17e`, PASS on lazy-deserialize alone).

## What this version covers

v1 PASSed the merged `lazy-deserialize` body. Since then one production commit
landed on the branch — `932564d6e` (coder v14's `Data.Load()` fix for auditor v1
F1) — and the `type-kind-strict` body itself (strict enforcement on reference
fundamentals, `IKindValidatable` / `IStrictKindEnforcer`, three-gate
`variable.set` chain) was never documented in `Documentation/v0.2/` — only
captured in bot reports. v2 closes both gaps.

## Changes

### `Documentation/v0.2/type-system.md`

New section: **"Strict kind — `as image/gif strict` validates wherever the bytes appear"**
(inserted after the existing "Typed values" block, before "Reader registry").
Documents:

- The two interfaces on `app.data` (`IKindValidatable`, `IStrictKindEnforcer`)
  and why they're separate (stateless sniffer vs. travelling imprint).
- Why markers live on `Data` (the dispatcher), not on `image.@this` (the
  implementor) — so strict's machinery depends on the marker only.
- Why `text` doesn't implement either (no content-based probe distinguishes
  plain text from markdown); `image` implements both (magic-byte sniff).
- The **three enforcement gates in `variable.set` and why none is redundant**
  (build / runtime probe / materialization imprint), each with its line range
  in `variable/set.cs`, mirroring the in-source annotation at lines 131-135.
- The strict×lazy chokepoint handoff to `Data.Load()` (with a cross-link to
  wire-serialization.md).
- Receiver-side discipline: wire-stamped `Strict=true` does **not** auto-impose
  strict — signing is the trust boundary; strict is a developer ergonomic
  (per `.bot/type-kind-strict/security/v1/result.md` F2).
- Forward-looking constraint surfaced by the security audit:
  **`IKindValidatable` public ctors must be side-effect-free**, because
  `TryInstantiateValidator` auto-invokes them on user data.

### `Documentation/v0.2/wire-serialization.md`

New subsection: **"`Data.Load()` — async pre-materialization at the serialize
chokepoint"** (inserted inside the `ISerializer returns Data` block, before
"http body dispatch"). Documents:

- Why the load must live **above** the STJ converter wall
  (`JsonConverter<T>.Write` is sync by the System.Text.Json contract).
- The full surface: `ILoadable.LoadAsync()` marker on the value;
  `Data.Load()` on the courier, returning `Task<@this?>` (null on success,
  `FromError` with key `StrictKindMismatch` on a strict trip).
- The graph walk: nested Data / dict / list, same `ReferenceEqualityComparer`
  cycle guard and `MaxNormalizeDepth` cap as `Normalize`. Tree-native leaves
  (string / `byte[]` / `ValueType` / null) short-circuit. No reflection walk
  over arbitrary domain objects.
- Why `ILoadable` is distinct from `IStrictKindEnforcer`: a lazy reference
  fundamental needs loading whether or not it is strict; strict piggybacks
  because `image.BytesAsync` is the load seam.
- Idempotence (an already-loaded image returns immediately).
- Call sites — first line inside the `try` of each `ISerializer.SerializeAsync`
  (`plang/this.cs`, `Json.cs`); `Text.cs` delegates non-primitives to Json so
  all three impls are covered through one of two files.
- Why this lives on `Data`, not on `ISerializer` (the walk is over Data's own
  value graph; putting it on the serializer would duplicate the walk per impl).
- The sync-readers-stay-sync split, with a pointer to the off-the-serialize-wall
  follow-up captured in `.bot/type-kind-strict/coder/v14/report.md`
  (`Width`/`Height`, `ValidateKind`'s `_ => Bytes` fallback).

### `Documentation/v0.2/good_to_know.md`

Two new index entries under "Section → doc":

```
- Strict kind — `IKindValidatable` / `IStrictKindEnforcer` / three enforcement gates → `type-system.md`
- `Data.Load()` — async pre-materialization at the serialize chokepoint → `wire-serialization.md`
```

## What was deliberately NOT changed

- **`CLAUDE.md`** — no new rule needed. The strict-kind story is a localised
  feature (one implementor today, planned audio/video) and the side-effect-free
  ctor discipline applies only to future `IKindValidatable` adopters — narrow
  enough that the in-doc note in type-system.md is the right home. If a
  third or fourth reference fundamental ships and footguns recur, escalate
  via `claude-md-proposals.md` then.
- **`docs/` (website)** — strict kind is a runtime/dev-side mechanism with
  no PLang-syntax surface beyond the existing `as <type>/<kind> strict`
  clause, which is documented in the module pages where it's reachable
  (`variable.set` examples). No new user-facing doc.
- **`Documentation/v0.2/data-internals.md`** — `Data.Load()` lives on `Data`
  but is an egress-side concern (it pairs with the serialize chokepoint, not
  with the materialization-on-touch story). Cross-linked from type-system,
  documented at length in wire-serialization; a data-internals duplicate would
  fragment the same story across three docs.
- **No bot summary file** — the auditor's PASS for the prior gap stands; this
  pass adds documentation, not new bot findings.

## Verification

- Cross-checked every claim against the source:
  - `PLang/app/data/ILoadable.cs`, `IKindValidatable.cs`, `IStrictKindEnforcer.cs`
  - `PLang/app/data/this.Load.cs` (full body)
  - `PLang/app/module/variable/set.cs:120-278` (three-gate chain + in-source
    annotation at 131-135)
  - `PLang/app/type/image/this.cs` (sole `IKindValidatable` /
    `IStrictKindEnforcer` / `ILoadable` implementor; `BytesAsync` throws
    `StrictKindMismatchException`; `LoadAsync => BytesAsync`)
  - `PLang/app/channel/serializer/plang/this.cs` and `Json.cs` (`await
    data.Load()` is the first line inside the `try` in both)
- Read prior bot artifacts (auditor v1+v2, codeanalyzer v3, security v1,
  tester v13, coder v13+v14) — every documented behavior matches the verified
  reports.
- No edits to `.bot/lazy-deserialize/**` or earlier `type-kind-strict/**`
  artifacts (history-preserving rule).

## Files

- `Documentation/v0.2/type-system.md` (new "Strict kind" section)
- `Documentation/v0.2/wire-serialization.md` (new `Data.Load()` subsection)
- `Documentation/v0.2/good_to_know.md` (two new index entries)
- `.bot/type-kind-strict/docs/v2/report.md` (this file)
- `.bot/type-kind-strict/docs/v2/verdict.json`
