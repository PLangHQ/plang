# Coder — compare-redesign

## Version: v3 (follow-up — reviewed architect's answers to the 7 v2 findings)

The architect settled all 7 v2 findings (commits `983b775ff`..`0a8f351b7`) and rewrote Stage 2 +
Stage 3. Ingi asked coder to read the answers and flag anything remaining. **Review only — no code.**
Deliverable: `v3/comments.md`. (v2 summary archived at `v2/summary_v2_archived.md`.)

**Verdict: 6 of 7 settled cleanly.** The `.`/`!` wire-split (2), the `path` demolition to private
`_location` (3), the narrow-on-examination identity chain (4), `data.Type → return _type` (5), the
path-math taxonomy (6), and typed-at-creation (7) are all well-grounded — several sharper than my
flags asked for.

**One substantive new finding (A):** finding 1's async-navigation safety check was under-verified.
It enumerated the list-module handlers + the `list:250` sort site, but missed two sync navigation
surfaces that reach `GetChild`:
1. **The source-generated lazy parameter getters are sync `get` accessors** (`PLang.Generators/
   Emission/Property/Data/this.cs:44,54,58`) that resolve `%a.b%` via `As<T>` → `Variable.Get` →
   `GetChild`. A C# property `get` *cannot* `await`. So a lazy param `Path = "%config.database%"`
   (config = a `read` reference) would read file I/O inside a sync getter — the `GetAwaiter().
   GetResult()` the plan forbids.
2. **`Variable.Get(string)` dotted-path (`variable/list/this.cs:570`, sync) + `Variable.Resolve`
   interpolation (`:649`, sync)** navigate the same way.
   The crux: narrow-on-examination defers the content read to *first touch*, but first touch is often
   a sync surface. The plan must name **where the async read boundary lands** so sync getters/resolver
   never trip into I/O — either eager read at `read`-time (parse stays lazy+sync), or an async
   pre-resolve pass before any sync getter sees the reference.

**Two secondary "add-a-sentence" notes:** (B) in-place `.Type` mutation on examination is a
read-causes-write — name the aliasing/clone/race semantics; (C) `name`-removal from the wire — grep
the read side (`FromWireShape`/nested-Data keying) before deleting.

Still **build it** — A is the last sharp edge on the async conversion, cheap to pin now.

---

## Version: v2 (review of architect's re-carved typed-value-model plan)

### What this is

The branch began as "redesign comparison" and grew (architect's pivot) into **the typed value
model**, with comparison as its first consumer. The architect re-carved the spine + 7 stages + 2
test docs after abandoning the earlier raw-CLR draft. Ingi asked coder to read the new plan/stages
and flag anything that doesn't make sense or won't work, in a file the architect can read.

This version is **review only — no code written.** Deliverable: `v2/comments.md`.
(v1 summary archived at `v1/summary_v1_archived.md` — it reviewed the abandoned raw-CLR draft.)

### What was done

Read `plan.md`, all 7 stage files, `plan/test-strategy.md`, `plan/test-coverage.md`, and grounded
every load-bearing claim against the real code on `compare-redesign`. Verdict: **build it** — the
spine is sound and works with the grain (the foundations it leans on already exist:
`item.Write(IWriter)`, the lazy `_raw`/`Materialize`/`FromRaw` rung, the `IOrderableValue` dispatch
the redesign replaces). The typed-model pivot also dissolved my v1 hazard (throw-on-GetHashCode
keying collision) — the value slot already holds the wrapper, no per-type raw-flip to sequence.

Flagged 7 concerns, grounded with file:line. The four to fix before implementation:

1. **Async value source regressed from a named sub-stage to one bullet** (a v1 win lost in the
   re-carve). Today `Materialize()` is *sync* (`this.cs:316`); the only async content load is
   per-type `ILoadable.LoadAsync()` (one impl, `image`). The door conflates async **read** (net-new)
   with sync **parse** (exists, navigation depends on it). Re-stage + draw the read/parse line.
2. **`!` plane redefinition collides with the existing `!` = Data-infrastructure meaning.** Today
   `GetInfrastructureValue` (`this.Navigation.cs:356`) resolves `!` against Data (Name/Error/Success/
   Properties); the plan repoints `!` to the *value's* type surface (`text!length`). `%text!length%`
   doesn't resolve today. Coexistence of Error/Success/Properties with value props is unspecified.
3. **`path` already holds `Content`/`Source` and serializes content-first** (`path/this.cs:169,177`).
   The "path = location, file = content" split is *demolition* on `path`, not addition. Stage 3
   reads additively.
5. **`item.ToRaw()` is load-bearing in `Data.Type`** (`this.cs:390`, CLR-mate derivation of a leaf),
   not just Pile-2 decompose sugar. Stage 6 deletes "generic ToRaw" — must name this core consumer.

Plus 3 "add a sentence" flags: (4) `read` returns lazy *content* today, not a `file` — note the bare
scalar contract; (6) Stage 7's "each step local" undersells 51 interior `.Relative/.Extension/
.Absolute` path-math sites; (7) the "door always returns a typed item" invariant isn't true today
(`_value` holds raw `string` for var-refs, raw `List`/`Dict` for containers).

### Scope counts gathered (for sizing)

- 23 `.ToRaw()` call sites (incl. the core `Data.Type` one).
- 51 `.Relative/.Extension/.Absolute` reads doing string math → Stage 7 gate ripple.
- 9 `path.Content` consumers → Stage 3 demolition surface.
- 11 types implement `IEquatableValue`/`IOrderableValue` → Stage 4/6 replicate list.
- 1 `ILoadable` impl (`image`) → confirms async-read path is net-new.

### Next

Architect reads `v2/comments.md` and decides which flags to fold into the stages. No code until the
async-source sub-stage and the `!`-plane coexistence are pinned.
</content>
