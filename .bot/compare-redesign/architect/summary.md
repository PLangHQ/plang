# Architect — compare-redesign

## 2026-06-08 — Pressure-test the comparison redesign and write the settled plan

Read the coder's plan (`.bot/compare-redesign/coder/compare-redesign-plan.md`), then pressure-tested it against the real code on this branch. Two scout passes had read the recycled `prevars-in-pr` state by mistake (the container reset mid-session, wiping fetched refs and flipping the branch); re-verified every load-bearing fact directly on `compare-redesign`.

Findings that moved the design, settled with Ingi:

- **Coercion by per-type rank, not "left operand coerces right."** The coder plan's rule 4 (each type owns its own coercion, driven by whichever operand is `a`) breaks antisymmetry — `text"10"` vs `number 9` gives opposite answers depending on order, corrupting `sort`. Fix: each type declares a rank (specificity; `text` is the floor); the higher-ranked operand's type drives, coerces the loser into its own kind, compares its own kind. This makes explicit what the symmetric `NormalizeTypes` bakes in today. Rank **lives on the type, not on Data** (Ingi's review comment): Data asks `this.Type.Rank(other)` — passing the whole other operand, never `other.Type` — and the type returns the winner. Data never compares ranks itself.
- **One async value door, lazy.** `await data.Value()` is the single accessor (returns `ValueTask<object?>` — sync-complete with zero alloc when present, async load only when pending, because it's the hottest accessor and ~990 sites). No public sync `.Value` property — just the private `_value` field. Lazy is the principle: a read/fetch holds only the path until `Value()` is first touched. Materialise is `source → _raw (I/O, async) → parsed (sync)`; `_raw` stays, and `ScalarValue` is renamed to `Peek()` — the sync "look at what's already here without parsing" read against `await Value()`'s "load and parse" ("scalar" pointed at the wrong axis).
- **The sync framework methods that can't be async, split by consequence.** `GetHashCode`/`Equals`/operators → throw with guidance (a wrong answer corrupts a dict; under this model collections key on the raw materialised value, not the wrapper). `ToString` → never throws, never does I/O: shows the present value or `<text pending>` (the debugger/logs render via `ToString`, and display tolerates not-loaded). No `#if DEBUG`, no `GetAwaiter().GetResult()`. Serialization materialises before the sync write boundary (already the codebase pattern), so the wire path never trips a throw.
- **Sync ordering core, async only at the edges.** `await data.Compare(other)` awaits both values then runs a sync ordering core. `sort` is two-phase — await keys (phase 1, all I/O here), order in-hand keys sync (phase 2). No `GetAwaiter().GetResult()` anywhere; a type's default compare must stay sync, I/O-bearing compares are expressed as `sort by <key>`.
- **Value lives once, raw, in Data; the type is a view over the Data** (`text(data)`, holds a pointer not a copy). The wrapper-stored-in-`Data.Value` shape goes away. This reverses the stored-wrapper half of scalars-as-native — the per-type classes become behavior views.
- **Enum semantics pinned.** `Incomparable` is ordering-only; equality across non-coercible non-null types errors; null is always comparable for equality (`== null` never errors).
- **Dispatch reuses the existing name→family routing** (`type.Convert` via `Conversions`); no new compare registry, no `Type.Name` switch.

Output: `plan.md` (the settled design, supersedes the coder plan's rules 1-2, 4, 8). No stage/test files yet — per Ingi, he reads and comments first.

Status: plan written, awaiting Ingi's comments.
