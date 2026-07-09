# Stage 1 progress state — item→host drop + kind redesign (uncommitted, resumable)

Big uncommitted change on `navigation-driven-record-builder` (two entangled pieces landed
together because they interlock; can't split to green). Compiles fully; core Read works;
Output/serialization has regressions to stabilise. **Not committed** (regressed). This doc =
where each piece stands so it resumes cleanly.

## DONE + working
- **item/ICreate drop** — goal/step/action/actions are plain C# hosts, flow as `clr<T>`;
  dead `IDataWrappable` deleted; ~40-file `Data<clr<T>>` cascade fixed (consumers use the
  existing sync `X.Clr<T>()`, since slots are resolved).
- **STJ readers → reflection Read** — goal + actions `.pr` load builds hosts via the `*`-kind
  `Read` (format-agnostic, `[Store]` walk, params through @schema:data). Round-trip DoD PASSES.
- **Output consolidation** — `reflection.Output` writes WireName (was `ToLowerInvariant` bug).
- **Kind redesign (A+C + "kind IS the behavior")** — COMPILES:
  - `behavior/` tree deleted; `kind.@this` (`app/type/kind/this.cs`) is the base owning verb
    defaults (`Navigate` re-derives node's kind per hop; `Descend`, `Enumerate`, `Set`, `Load`,
    `Convert`, `Output`, `Clr`, `WriteReflected`).
  - kinds moved under their type: `app/type/item/kind/{json,list,dict,reflection}/this.cs`
    (class `@this`). list kind owns index-descend, dict key-descend, `*` properties-only.
  - one door `app/type/kind/list/@this` = `App.Type.Kind[name|clrType]` (exact ClrForm →
    assignable → `*`; unknown name → base instance; born with context). `Kinds`→`Kind`,
    `kind.Of` + the string implicit deleted; all ~18 sites fixed.
  - recursion crash fixed (`WriteReflected` gated on `Tagged.IsTagAware` + collection dispatch).

## REGRESSED — needs fresh careful work
- **Output over-reflects infra.** The `*` kind is now the universal Output for any clr POCO, so
  runtime graphs reach a `context.@this`/`dict` that can't serialize → `NormalizeException`
  ("json.Writer received app.actor.context.this") and a dict-through-STJ read error. Root: more
  objects route through `reflection.Output` than the old `OutputTagged` ever did. This is the
  main thing to fix — likely narrow what the `*` kind reflects, or stop these objects reaching it.
- **Full-suite delta elevated** vs the 129 baseline: Modules ~76, Data ~52, Wire ~20, Types ~22,
  Runtime ~13 (Runtime aborts early on the context crash — total 288 vs 750, so its real count is
  hidden). Generator green.
- **Pin test write path** (`ClrJsonActionsWriteTests`) still asserts `Actions.Count == 0` — a
  specific navigate-then-set trace on `%goal.Steps[0].Actions%` (clr<goal> stored). Round-trip
  Read is green, so it's the WRITE (SetValueOnObject → clr arm → `*`-kind Set) path.

## OWED cleanups (marked in code + todos.md 2026-07-09)
1. `WriteReflected` obpv → collapse to `new Data(value).Output()` (Data.Output does bare/envelope
   by writer format).
2. `variable.set` `CanonicaliseKind` → fold into the `Kind[name]` door (late stamp).
3. `Format.TypeFromMime` — review (Ingi flagged).
4. `Tagged.IsTagAware` obpv → `HasAttribute`-style; the four `IsDefined` checks are duplicated
   (this method + `Tagged.Compute`).

## Resume plan
1. Fix Output over-reflection (find which objects reach `reflection.Output` that shouldn't; the
   `*` kind should reflect only genuine hosts/[Out] wire objects, not runtime infra).
2. Fix the pin-test write trace.
3. Drive the full suite back to ~baseline; then commit the whole thing.
4. Then the 4 owed cleanups.
