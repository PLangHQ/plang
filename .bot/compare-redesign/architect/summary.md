# Architect — compare-redesign

## 2026-06-08 — Coder v1 review responses (folded into plan + stages)

The coder reviewed the plan + six stages + test docs against the real code (`.bot/compare-redesign/coder/v1/comments.md`) — verdict "build it," with grounded gaps. All addressed:

1. **Async source is net-new, not wiring (biggest).** `_source`/`await ReadAndParse()` doesn't exist; today there's sync `_valueFactory`, sync `_raw`+`Materialize`, and per-type `ILoadable.LoadAsync()`. → Added **Stage 2 Part A** "the async value source" (the bulk): names the shape (`ValueTask` source the door awaits), what collapses into it, and the `ILoadable` fold as the one fork. Plan index + test-coverage surfaces updated.
2. **~990 overcounts; migration isn't mechanical.** ~74 are `Lazy`/`KeyValuePair`/`Nullable`/`JsonElement`, and the views own `.Value` too. → Re-scoped to **`Data`-receiver `.Value` only**; **views keep a sync `.Value`** (the present-value read); stopping rule is by receiver type, not a find-replace.
3. **Stages 2–4 aren't independently green.** → Stated outright: **2–4 are one green unit, green at the 2→4 boundary**; plan index gate amended; stage 2/3/4 deps note it.
4. **Throw-on-`GetHashCode`/`Equals` collides with live keying.** → Added per-type sequencing: the throw and the raw-flip ship **together per type** (distinct axis from the mediator coexistence).
5. **`contains` on `Incomparable` element — error or not-found?** → Pinned: **membership (`contains`/`in`/`indexof`/`unique`) treats `NotEqual`/`Incomparable` as no-match, never errors** — only the comparison operators + sort error. Added a membership column to Stage 1's boundary table; updated Stage 5 + test-coverage.
6. **`Value` virtual override seam lost.** → The override seam moves to the **source / protected `Load()` hook**, not an overridden property; named in Stage 2 Part A.

Smaller: `Peek`/`Diff` renames confirmed clean; `path` default-compare-stays-sync made explicit (vs its truthiness going async); `ValueTask` await-once backed by an analyzer/grep gate, not just prose.

## 2026-06-08 — Pressure-test the comparison redesign and write the settled plan

Read the coder's plan (`.bot/compare-redesign/coder/compare-redesign-plan.md`), then pressure-tested it against the real code on this branch. Two scout passes had read the recycled `prevars-in-pr` state by mistake (the container reset mid-session, wiping fetched refs and flipping the branch); re-verified every load-bearing fact directly on `compare-redesign`.

Findings that moved the design, settled with Ingi:

- **Coercion by per-type rank, not "left operand coerces right."** The coder plan's rule 4 (each type owns its own coercion, driven by whichever operand is `a`) breaks antisymmetry — `text"10"` vs `number 9` gives opposite answers depending on order, corrupting `sort`. Fix: each type declares a rank (specificity; `text` is the floor); the higher-ranked operand's type drives, coerces the loser into its own kind, compares its own kind. This makes explicit what the symmetric `NormalizeTypes` bakes in today. Rank **lives on the type, not on Data** (Ingi's review comment): Data asks `this.Type.Rank(other)` — passing the whole other operand, never `other.Type` — and the type returns the winner. Data never compares ranks itself.
- **One async value door, lazy.** `await data.Value()` is the single accessor (returns `ValueTask<object?>` — sync-complete with zero alloc when present, async load only when pending, because it's the hottest accessor and ~990 sites). No public sync `.Value` property — just the private `_value` field. Lazy is the principle: a read/fetch holds only the path until `Value()` is first touched. Materialise is `source → _raw (I/O, async) → parsed (sync)`; `_raw` stays, and `ScalarValue` is renamed to `Peek()` — the sync "look at what's already here without parsing" read against `await Value()`'s "load and parse" ("scalar" pointed at the wrong axis).
- **The sync framework methods that can't be async, split by consequence.** `GetHashCode`/`Equals`/operators → throw with guidance (a wrong answer corrupts a dict; under this model collections key on the raw materialised value, not the wrapper). `ToString` → never throws, never does I/O: shows the present value or `<text pending>` (the debugger/logs render via `ToString`, and display tolerates not-loaded). No `#if DEBUG`, no `GetAwaiter().GetResult()`. Serialization materialises before the sync write boundary (already the codebase pattern), so the wire path never trips a throw.
- **Sync ordering core, async only at the edges.** `await data.Compare(other)` awaits both values then runs a sync ordering core. `sort` is two-phase — await keys (phase 1, all I/O here), order in-hand keys sync (phase 2). No `GetAwaiter().GetResult()` anywhere; a type's default compare must stay sync, I/O-bearing compares are expressed as `sort by <key>`.
- **Value lives once, raw, in Data; the type is a view over the Data** (`text(data)`, holds a pointer not a copy). The wrapper-stored-in-`Data.Value` shape goes away. This reverses the stored-wrapper half of scalars-as-native — the per-type classes become behavior views.
- **Enum semantics pinned (sharpened while writing stages).** `NotEqual` = reconciled-but-unequal-and-unordered (equality ops use it, ordering ops error). `Incomparable` = couldn't-reconcile-at-all (every op errors). That split is what makes `dict == number` error while `dict == dict` works and `%x% == null` works. (Corrected the earlier "Incomparable is ordering-only" wording — it can't be literally true if `dict == number` must error.)
- **Dispatch reuses the existing name→family routing** (`type.Convert` via `Conversions`); no new compare registry, no `Type.Name` switch.
- **Rank returns the driving type; ordering is in caller order (no sign flip).** `this.Type.Rank(other)` returns the driving type (higher-ranked), and `driver.Order(a, b)` compares `this`-vs-`other` in caller order — so `Less` always means `this < other`. (Caught while writing the dispatch: ordering winner-vs-loser and flipping afterwards is a latent sign bug; avoided by ordering in caller order.)

Output: `plan.md` (the spine, supersedes the coder plan's rules 1-2, 4, 8), six stage files at root, and `plan/test-strategy.md` + `plan/test-coverage.md`.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [Comparison enum](stage-1-comparison-enum.md) | pending |
| 2 | [Value door + value-as-raw flip](stage-2-value-door.md) | pending |
| 3 | [Per-type rank, coercion, sync ordering core](stage-3-per-type-compare.md) | pending |
| 4 | [Data.Compare entry](stage-4-data-compare.md) | pending |
| 5 | [Move consumers](stage-5-consumers.md) | pending |
| 6 | [Demolition + Diff rename](stage-6-demolition.md) | pending |

Status: plan + stages + test docs written. Two precision fixes made while carving stages (enum NotEqual/Incomparable split; rank returns driving type / no sign flip) — flagged to Ingi.
