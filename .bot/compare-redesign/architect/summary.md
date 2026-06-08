# Architect — compare-redesign

## 2026-06-08 — `dir.list : list<path>` resolves directory serialization (spine updated)

Traced `write out %dir%` and hit a real problem: "each item serializes itself" + "a `file` serializes as its content" would make a directory dump every file's *content* instead of a listing. Resolution (Ingi's): a directory's listing is **`list` : `list<path>`** (renamed from `Entries`) — a list of *locations*, not content-bearing files. A `path` has **one serialization** (its location string), so `write out %dir%` → serialize `list<path>` → location strings → a clean (flat) listing; a content-bearing `file` only exists when you explicitly `read` a path. Invariant: **listings/structures hold `path` (locations); content comes only from `read`.** This makes the traced "subject vs nested" context-bit **unnecessary** (a `path` has a single face). Corrected rule 8 accordingly. Also addressed review comment 75c54f319f: marked `%!actor%` (root traversal via `!`) as a conceptual/future example, **not** for the coder to implement on this branch (the existing `%!app%`/`%!data%` stand).

## 2026-06-08 — Navigation planes + the meta-rule (spine updated)

Long design session settled the navigation/serialization model and a governing principle. Folded into `plan.md`:
- **The meta-rule: the type decides.** No central taxonomy of value cases — each type owns its navigation, property surface, serialization, compare. Concrete types are examples, not a frozen case-table (the existing OBP "behaviour on the element" line). The plan now states *mechanisms* and shows types as examples.
- **Two access planes — `.` and `!`.** `.` = data plane (navigate content/fields/keys/elements); `!` = the value's typed property plane (`%list!count%`, `%text!length%`, `%x!type%`, `%file!size%`) — and `!` **is the Pile-3 surface**. Leading `!` = the property plane against the implicit root (`%!app%` ≡ `%this!app%`). `!` already exists in the grammar (not new syntax); no clash with `if !%x%` (LLM → `module.condition` at build). The sigil picks the plane, so a content key `size` (`.size`) never shadows the property (`!size`).
- **Kinds are not values.** json/csv/xml are *kinds* + deserializers that produce an `item` (dict/list); we never work with "json," only items. So `%x.type%` is a content field, `%x!type%` is the value's PLang type — different planes, no special-casing.
- **A reference is stable; only its content facet refines.** `file`/`url`/`image` are stable (path + metadata + lazy content facet) — `%file!size%` always works; the content facet materialises within. "Refine-and-replace (`bytes → item`)" describes a *bare* value, not a reference. Corrects the earlier rule-5.
- **`write out %x%` is type-owned serialization** (OBP rule 9) — the type's `Write` decides its wire shape; `file` → content, `directory` → its `Entries` (each entry self-serializing, recursive), scalar → itself. No universal "forward to `!content`" (that's just `file`'s choice).
- **`directory`** has `Entries` (its listing), not "content" (content is the wrong word for a dir).

## 2026-06-08 — Scope grew to the typed value model (whole thing on this branch)

Settled the remaining model decisions and the branch's true scope: this is no longer "redesign comparison," it's **the typed value model**, with comparison as the first consumer. Decisions:
- **`file` + `directory` + `url` are new types**, in a reference-fundamental hierarchy `path → file (path+content+metadata) / directory (path+entries) / url (remote http/s3/ftp + fetched content + metadata) → image/audio/video (file specialisations)`. `read X` → a `file` (local) or `url` (remote); unknown local → generic `file`. `url` over `uri` (it locates a fetchable resource); reuses the existing `path.scheme` registry (today's `HttpPath` ≈ `url`). `image` becomes a `file` specialisation. (Review comment 114f86ef78.)
- **`write out %file%` writes the file's content** (intent-based — "write the file," not its properties); the file's wire form / `Write` is its content (the `image` precedent). Metadata is navigable (`%file.size%`, `%file.path%`). `text` stays pure content — no `.Path`.
- **No generic `ToRaw`** — raw CLR is private; it leaves a type only via the type's own `Write(IWriter)`, `As<T:item>` conversion, and gated per-type interop accessors (`path.Absolute`-style, enforced like the `System.IO` gate). `text.Value` (public raw string) goes private too.
- **`As<T>` constrained `T : item`** (type→type, never CLR).
- **Pile 3 — full public-surface typing** (`path.Absolute → path`, `text.Length → number`, every CLR-returning public member → PLang type) is **in this branch, as the final stage**, ridden behind a build gate so it converges.
- The async/lazy `ValueTask` door, one-representation-at-a-time (no `_raw`), and all carried-over comparison decisions (enum/rank/caller-order/two-phase-sort/membership/renames) stand.

Branch stays `compare-redesign` (rename to `typed-value-model` deferred — would move all `.bot/` output + re-push right after a crash, no real gain). Rewrote `plan.md` as the typed-value-model spine with a 7-stage index (1–6 = value model + comparison; 7 = the surface-typing bulk, last). Stages + test docs to be carved into files next.

Container crashed mid-session; nothing lost — last push (`6b1775c3c`, the typed-model pivot) was intact on the remote; restored the branch from origin.

## 2026-06-08 — Pivot: the type holds the value (raw-CLR model abandoned)

Ingi reconsidered the foundational choice and committed to the **typed** model: the value slot holds the PLang typed value (`text`/`number`/`binary`/`dict`/…), `data.Value()` returns it, and **raw CLR is the leaf exception** (`ToRaw()` at typed-conversion returns / interop / the writer). This reverses the abandoned draft's rules 1–2 ("value is raw CLR, type is a view") and re-aligns with where `scalars-as-native` was already heading. It's also *less* work: no "value-as-raw flip," no view construction in compare — the value already owns its behaviour (the existing `item.Write`, `AreEqual`/`Order` are the foundation). The decisive signals: `item.@this.Write(IWriter)` (value owns its wire shape) and the compare mediator's `is IOrderableValue` dispatch both assume a typed stored value; raw was fighting the grain.

Rung model settled (Ingi's file-read question): the value is **one representation at a time, refine-and-replace** — `path → binary/text → dict`. `_raw` dissolves into the `binary`/`text` rung (no special byte slot). Drop the prior on refine (no double storage). **Verbatim passthrough = the never-parsed path** (read → write-out without navigating stays binary/text → original bytes); **display is passthrough, not a parse** (refine to dict only on `%x.field%` navigation). Rare inspect-then-forward-original is explicit (keep the bytes separately).

Actions: deleted the six raw-model stage files and the two raw-model test docs; rewrote `plan.md` as the typed-model spine. Carried over unchanged: the `Comparison` enum + boundary mapping, rank-on-the-type + caller-order ordering, two-phase sort (no `GetAwaiter().GetResult()`), membership-never-errors, the `Peek`/`Diff` renames. Stages + test docs to be re-carved once Ingi approves the new spine.

Open before re-carving: confirm raw-CLR access is bounded to leaves (sample a few handlers); `number` boxing acceptable; the unified `IComparableValue` shape + where rank is stored.

## 2026-06-08 — Coder v1 review responses (folded into plan + stages)

The coder reviewed the plan + six stages + test docs against the real code (`.bot/compare-redesign/coder/v1/comments.md`) — verdict "build it," with grounded gaps. All addressed:

1. **Async source is net-new, not wiring (biggest).** `_source`/`await ReadAndParse()` doesn't exist; today there's sync `_valueFactory`, sync `_raw`+`Materialize`, and per-type `ILoadable.LoadAsync()`. → Added **Stage 2 Part A** "the lazy value source" (the bulk): one source, **one chain** `reference → read (async) → raw → parse (sync) → value`. Per Ingi's later ask, flows 2 and 3 are **merged into that one mechanism** (no "fold ILoadable" fork left): `_raw`+`Materialize` = the parse step, `ILoadable` = the read step, authored values skip both, `DynamicData` is the sync recompute case. Plan index + test-coverage surfaces updated. (Birth point — `FromRaw` — deferred to a follow-up discussion.)
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
- **Value lives once, raw, in Data; the type is a view over the Data** (`text(data)`, holds a pointer not a copy). The wrapper-stored-in-`data.Value` shape goes away. This reverses the stored-wrapper half of scalars-as-native — the per-type classes become behavior views.
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
| 4 | [data.Compare entry](stage-4-data-compare.md) | pending |
| 5 | [Move consumers](stage-5-consumers.md) | pending |
| 6 | [Demolition + Diff rename](stage-6-demolition.md) | pending |

Status: plan + stages + test docs written. Two precision fixes made while carving stages (enum NotEqual/Incomparable split; rank returns driving type / no sign flip) — flagged to Ingi.
