# codeanalyzer v1 plan — runtime2-data-share-state

## What I'm looking at

Coder/v1 landed phases 1–4 + 5a spot-check tests of architect/v1's 6-phase plan
(Phases 5b/5c/6 deferred — need rebuilt `.pr` files). Files I will review in
depth (the rewrite epicenter):

1. `PLang/App/Data/this.cs` — events→Lists, `WrapAs<T>`, `AsCanonical`,
   `IsPlangIterable`/`IsPlangAssignable`, `TryFullVarMatch`, `AsT_Impl`
   rewrite for identity preservation.
2. `PLang/App/Variables/this.cs` — `Set` cleanup; `Remove` fires `OnDelete`.
3. `PLang/App/Debug/this.cs` — `+=` → `.Add(...)` for the new List<Action> shape.
4. `PLang/App/modules/variable/set.cs` — full rewrite: `MintTyped` if-chain +
   reflection fallback, `CarryStateFromSource`, `SnapshotClone` via JSON
   roundtrip with `UnwrapJsonElement`.
5. `PLang.Generators/Emission/Property/Data/this.cs` — plain-Data emission uses
   `AsCanonical`.

Plus a wider sweep across all module action handlers to satisfy Ingi's lifecycle
audit.

## Two analysis lenses

### A. The standard 5-pass codeanalyzer review
Pass 1 OBP, Pass 2 simplification, Pass 3 readability, Pass 4 behavioural,
Pass 5 deletion test. Output per the character file format.

### B. Ingi's special lens — Data lifecycle / zero overhead

For this branch specifically, Ingi wants me to follow Data through the whole
pipeline:

- **Where is Data created?** Map every site. Each site must be either
  (a) `variable.set` (the sole binding-mint site by design),
  (b) a same-type fast-path return (no construction),
  (c) a legitimate cross-type cast in `WrapAs<T>` / `AsCanonical`, or
  (d) error sentinel (`Data.FromError`).
  Anything else is a finding.

- **Where is `.Value` read?** `.Value` should be unwrapped only inside
  module action handlers (the leaves). Engine, runtime, generator, and
  property emission must never read `.Value` for any business-logic purpose
  (only for reflection-based type inference inside variable.set, which
  itself is a leaf module). I will grep for `.Value` outside `Runtime2/actions/`
  and `App/modules/` and triage every hit.

- **Data-in-Data?** Any `new Data<Data<...>>(...)` or `new Data(<Data instance>)`
  is a bug. Will grep for the patterns and verify wraps return the underlying
  source ref.

- **Redundant copies?** A "copy" means `new @this(...)` or
  `new @this<T>(...)` constructed when the source's identity could have been
  preserved (same-type fast path). Will count `new @this` constructions in
  `Data/this.cs` and confirm each one is justified by:
  - cross-type conversion (Rule 3),
  - variance with cast-only (Rule 2),
  - a `variable.set` mint (the only legitimate fresh allocation), or
  - explicit clone semantics from `variable.set` source carry.

## Output

- `result.md` — full per-file findings in the character-file format.
- `verdict.json` — pass/fail + one-line summary.
- `summary.md` — version summary in the bot format.
- `summary.md` at the bot root — light cross-session summary.

## Verdict criteria

- **CLEAN/pass**: no rule violations or major silent bugs; minor nits only.
- **NEEDS WORK / fail**: simplifications worth doing or data-lifecycle bugs.
- **MAJOR ISSUES / fail**: OBP rule violations or behavioural bugs in the
  identity-preservation contract.
