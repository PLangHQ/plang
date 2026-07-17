# goal-graph-singular — plan

Settled with Ingi 2026-07-17. Foundation: `architect/map.md` (the full touch map with code — read it first; this plan sequences it). Decisions folded: **D1 = rename (everything singular, including the LLM schema keys); D2 = handler params ALSO singular; D3 = `build.actions` left alone** (it dies in module-discovery 6c — renaming a corpse isn't cleaner; the one temporary plural is a dying name with a named executioner).

> **You (coder) own the final code and test shape.** The map's code blocks pin intent.

## Why

The goal graph is the runtime's spine and it violates the language's own naming law: plural folders (`steps/`, `actions/`, `modifiers/`), plural properties, plural wire keys — while the rest of the system settled on singular + collection-node (`app.module`, `X/list/this.cs`). The inconsistency is now load-bearing in the worst way: `steps.@this` pretending to be a CLR `IList<Step>` is what caused the layer-4 `item.Create ⇄ type.Create` bounce, and every plural/singular seam (graph, params, LLM schemas) is a place where vocabulary splits. One pass makes the language singular end-to-end — classes, wire, authored goals, LLM contract — and fixes the bounce as a consequence, not a patch.

## Areas, in landing order

**0. Baseline.** Full-suite by-name snapshot on this branch; one real `plang build` (Sanity) with the trace's prompt strings saved as reference (the schema keys change in area 3 — the pre-change prompts are the eyeball baseline).

**1. The graph rewrite** (map §A/§B/§C/§F — the core):
- The three collection classes rewritten as `list.@this` subtypes at the convention slots (`goal/step/list/`, `goal/step/action/list/`, `goal/step/action/modifier/list/`) — full member accounting in map §A; born-with-context (the late-set `Context` dies); the `IList<T>` facades keep C# consumers compiling.
- Element property renames: `goal.Step`, `step.Action`, `action.Modifier`, **`goal.Goals` → `goal.Child`** (D4 — the `Parent`-symmetry name; wire `child:`), `action.Parameters` → `action.Parameter` (pulls PLang.Generators' `GetParameter` surface into the rename), `step.Warnings` → `step.Warning` — the wire follows automatically (WireName = camelCase of the property; zero serializer code). The inventory commit closes the list (map A0.3 — these three were missed by the first sweep; assume more until the inventory says otherwise).
- Namespaces `app.goal.steps.step.actions.*` → `app.goal.step.action.*` (99 files), 6 aliases, the 3 real literals (`node?["steps"]` ×2 → `["step"]`; `record["modifiers"]` → `"modifier"`).
- `ContainerFamily` drops the `IList<>` probe; the exact-generic residue aligns claim=build (map §F). The apex gains zero rungs.
- `action.list` gains its ICreate face (the builder's `set %goal.Step[i].Action%` is a real list→collection creation).

**2. The handler-param singular sweep** (Ingi's D2 — NEW area, its own inventory first):
- Inventory: grep every action-handler and settings-node property with a plural name (`Actions`, `Files`, `Headers`, `DefaultHeaders`, `Messages`, `Tools`, `Parameters` on mock, …) — the inventory commit lists them ALL with per-name dispositions before any rename. Non-plural names (`Include`, `Exclude`, `Body`, …) untouched.
- Rename the properties; the generator regenerates bindings; the LLM-facing param vocabulary follows via the catalog (rows read property names).
- **The `.pr` param names regenerate via the full rebuild** (area 4) — the migration script does NOT rewrite param-name values (that would need a per-action rename table; the rebuild does it correctly for free).
- **CLI surface changes ride along and are USER-FACING**: `--build={"files":[…]}` → `{"file":[…]}`, `--test={…}` keys per renamed settings props — `cli_reference.md` + help text update in the same commits. Flag each in the inventory.
- LLM schema keys singular (D1): `Plan.goal:26` `steps:` → `step:`; `QueryAndVerify:46`/`FixValidation:63` `actions:` → `action:` (and the two schemas UNIFY while touched — the known inconsistency). LLM cache busts by content — expected, noted, not fought.

**3. os/ authored surface** (map §D): the 10 files — `%goal.Steps[…]%` → `%goal.Step[…]%`, template member renames, `planStep.actions` → `planStep.action`, the schema keys above.

**4. The `.pr` migration + full rebuild** (map §E): the one-shot key-rename script over the 806 tracked files — **its key set is EVERY renamed `[Store]` wire key, closed by the inventory** (`steps→step`, `actions→action`, `modifiers→modifier`, `goals→child`, `parameters→parameter`, `defaults→default`, + whatever the inventory adds; verify whether `errors`/`warnings` are `[Store]` at all) — because bootstrap-readability is the job: a missed key (e.g. `parameters:`) means the new reader loads actions without params and the bootstrap build can't run. Param name VALUES (inside parameter rows) still ride the rebuild, not the script. Then: new binary builds green → **full `plang build` regenerates everything** → diff reviewed → the script is deleted. No both-keys compat reader.

**5. Acceptance.**
- The layer-4 repro (Fluid `{% for step in goal.step %}`) green; layer-3 unaffected; `Nest` suite green.
- Full suite by-name vs the area-0 baseline: zero unexplained.
- One real `plang build` end-to-end; compile-quality eyeball vs the area-0 prompt references (the schema keys changed — this is the gate that catches an LLM regression).
- Hot-path perf: step iteration measured vs baseline (the facade-over-rows cost; cache if it shows).
- Grep gates: `app.goal.steps`, `\.Steps\b`, `\.Actions\b`, `\.Modifiers\b`, `"steps"`/`"actions"`/`"modifiers"` wire keys in production + os + the inventoried plural param names → zero (D3's `build.actions` the one named survivor, with its 6c death note).
- Merge back into `get-builder-running`.

**1b. The property promotions from the plang-type audit** (map "Third pass"): `step.Error`/`action.Error` as `error.list` (`Add(IError)`, callers hand the error they hold — no flattening); `step.Warning`/`action.Warning` as `warning.list` (**`Info` replaced by `Warning`**; shared error/warning face = an interface only if a uniform consumer exists — coder checks, doesn't pre-build); `action.Parameter`/`action.Default` as native lists (**the generator's `GetParameter` moves to the native-list row lookup**); backrefs STAY host references (the 0-sets/hundreds-of-reads trace, on record in the map); scalars stay CLR — each slot-write is the sanctioned crossing per the test in the audit section.

## For the comment round — where your knowledge beats the trace

1. **The `private protected _items` seam** — smallest seam I found for the facades; if you see a cleaner storage access (or a reason the seam leaks), say so before building three classes on it.
2. **The migration script's key set** — area 4 lists it; you close it with the inventory. Anything key-renamed but missed by the script = a silent bootstrap break; propose the verification (a post-script grep for the old keys across the 806?).
3. **Hot-path measurement** — pick the method (a Sanity-goal run timed pre/post? the Types suite wall-clock?) and the threshold that triggers the typed-cache fallback.
4. **`errors`/`warnings` `[Store]` status** — my grep didn't show attributes on those four properties; if they're not wire-stored, the script skips them and the collections change is C#-only.
5. **The LLM schema/param renames** (areas 2-3) — you're closest to the compile-quality risk; if the prompt-reference eyeball needs to be a harder gate (a golden per template), upgrade it.
6. **Anything in map §A's member accounting that doesn't survive contact** — the map is traced but the code wins; flag contradictions rather than following it.

## Demolition (must NOT survive)

- `goal/steps/this.cs`, `…/actions/this.cs`, `…/modifiers/this.cs` (the classes at the old slots), the plural folders themselves, `_items` private storage ×3, the parameterless ctors + late-set `Context` ×3.
- **`app/Info.cs` (the class — replaced by `Warning`), the four `List<Info>` properties, and every site flattening a real error into an Info** (the producer hands its `IError` to `error.list.Add` instead).
- `actions.Value` (the raw-storage leak), `ComputeBranchChain`/`SplitAtConditions` (renamed `Chain`/`Branches`).
- Aliases `GoalSteps`/`StepActions`/`ActionModifiers` (replaced or re-pointed).
- `ContainerFamily`'s `IList<>` interface probe.
- Every inventoried plural handler-param property name.
- The migration script itself (one-shot; dies in-branch).
- The old `.pr` plural keys — no reader compat arm, anywhere.

## Stays

- `Include`/`Exclude` and every non-plural param name; `Test.Include`'s typed-generic declaration (parked settings pass).
- `build.actions` the ACTION name (D3 — module-discovery 6c deletes it; note in its file).
- `GetTypeName`-side broad naming (naming ≠ construction claims — map §F).
- Property wire derivation (Tagged camelCase) — untouched, it's what makes the rename carry the wire.
- The `Goal`/`Step` backref stamping semantics, `RunAsync` bodies, `MergeFrom` semantics — bodies ride, identifiers change.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| collections as `list.@this` subtypes at `X/list` slots | accepting classes hold plang types; convention node; bounce dead at rung 1 | ok |
| singular everywhere incl. wire + LLM keys | one vocabulary end-to-end; no seam at the LLM | ok |
| born-with-context ×3 | the late-stamp smell dies with the rewrite | ok |
| migration = script (bootstrap) + rebuild (truth) | generated artifacts regenerate; no compat fork in the reader | ok |
| D3 survivor named with executioner | no churn on a dying name; the exception is line-itemed | ok |

## Plang-type leaf audit

The rewritten collections ARE plang values (the point). Rows store raw hosts (lift-on-read — `list<clr<step>>` per the parent plan). No new CLR leaves introduced; the CLI settings keys rename but their VALUES bind exactly as before (`Build.File` stays the native plang list per the settings ruling). Flag any new scalar the implementation adds that answers a raw CLR name.

**The sanctioned-crossing test (Ingi, 2026-07-17 — apply it to every slot this plan touches):** *does the destination genuinely require CLR — its own storage, or a 3rd-party API? Then the value crosses ITSELF, once, at that edge (`value.Clr(target)` at the slot; lower-on-write symmetric with lift-on-read). Could the destination have held the plang value? Then lowering was the violation — promote the slot instead.* This is why: collections/settings promoted (could hold plang); host scalars (`Text`, `Index`, `Indent`, bools) stay CLR and cross at their slot (the host IS the C# side; the boundary sits where crossings are fewest); banned shapes stay banned (courier `data.Value as X`, mid-flight lowering, call-site pre-decomposition).
