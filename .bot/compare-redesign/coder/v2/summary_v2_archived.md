# Coder — compare-redesign

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
