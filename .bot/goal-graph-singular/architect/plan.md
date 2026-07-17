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

**4. The `.pr` migration + full rebuild** (map §E): the one-shot key-rename script over the 806 tracked files (graph keys ONLY — its job is bootstrap-readability, nothing else) → new binary builds green → **full `plang build` regenerates everything** (params get their new names here) → diff reviewed (expected changes only) → the script is deleted. No both-keys compat reader.

**5. Acceptance.**
- The layer-4 repro (Fluid `{% for step in goal.step %}`) green; layer-3 unaffected; `Nest` suite green.
- Full suite by-name vs the area-0 baseline: zero unexplained.
- One real `plang build` end-to-end; compile-quality eyeball vs the area-0 prompt references (the schema keys changed — this is the gate that catches an LLM regression).
- Hot-path perf: step iteration measured vs baseline (the facade-over-rows cost; cache if it shows).
- Grep gates: `app.goal.steps`, `\.Steps\b`, `\.Actions\b`, `\.Modifiers\b`, `"steps"`/`"actions"`/`"modifiers"` wire keys in production + os + the inventoried plural param names → zero (D3's `build.actions` the one named survivor, with its 6c death note).
- Merge back into `get-builder-running`.

## Demolition (must NOT survive)

- `goal/steps/this.cs`, `…/actions/this.cs`, `…/modifiers/this.cs` (the classes at the old slots), the plural folders themselves, `_items` private storage ×3, the parameterless ctors + late-set `Context` ×3.
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
