# Stage 4 — module-discovery — plan (v2)

Spec: architect `plan.md` §"Stage 4 — module-discovery". No dedicated stage4 answer doc — the
plan is the spec (concrete enough on shape).

## Goal

Replace `module.Describe()` (imperative reflection → `StepActions`) with **value-object views**:

- `app.module.list : list<module>` — the module collection (mirror `type.list`/`goal.list`).
- `module.Actions : list<action>` — each module's actions.
- `action.Properties : list<type>` — each action's parameter properties, keyed by name.

**Views over the handler classes** — reflection happens ONCE, at the `action` view leaf (unwrap
`Data<T>`/`[Code]T` → plang type). Consumers read `type.Name`, never a `System.Type`, never a
`GetTypeName` at a call site. Then Fluid `.md` templates + builder goals render the prompt; delete
`Describe()`, `StepActions`, `BuildTypeEntries(modules)`.

## Baseline (Stage-4 start)

Whole suite ~**224** reds (snapshot post-NRE-fix). Stage 4 must not increase it. Remaining reds
are characterized in `coder/stabilization-findings.md` (text→choice, http provider-null, etc.) —
those are separate, awaiting architect ruling; not Stage-4 regressions.

## What `Describe()` does today (the contract to preserve — teaching parity)

`module/this.cs:297`: for each `ns` in `Names`, each `actionName` in `GetActions(ns)`, take
`GetActionType` → walk public instance properties, skipping `EqualityContract`, capability-
interface props (`IContext/IStep/IChannel/IEvent/IStatic`), and `[Code]` props; unwrap the CLR
type → plang type name (`GetTypeName`), append `?` for nullable; attach per-action markdown
teaching (`MarkdownTeachingRoot`). Output = `StepActions` (module → action → parameter metadata).

The views must expose the SAME structure so the Fluid template reproduces the prompt.

## Decomposition (dependency order)

- **4a — `module` value + `app.module.list : list<module>`.** New value type `module` (name +
  its actions), collection hand-rolled like `goal.list` (mutable — `code.load`/`Discover` add at
  runtime). `app.module` news it once; `app.Module` stays the registry surface during migration.
- **4b — `module.Actions : list<action>`.** `action` value = name + owning module + the handler
  `System.Type` (private; never leaks). Reflection deferred to 4c.
- **4c — `action.Properties : list<type>`.** The reflection leaf: walk the handler's properties
  with the SAME filters as `Describe()` (capability ifaces, `[Code]`, `EqualityContract`), unwrap
  `Data<T>`/`[Code]T` → the property's plang `type` entity; keyed by property name. Consumers read
  `type.Name`.
- **4d — Fluid templates + builder goals.** `os/system/builder/templates/modules.md`; builder
  goals `get all modules → %modules%`, `ui.render 'templates/modules.md' → %doc%`. Structure from
  the views; prose from the existing `os/system/modules/<module>/*.md`. Drill-in via filter.
- **4e — migrate callers + delete.** Repoint `Describe()`/`StepActions`/`BuildTypeEntries(modules)`
  callers (type/this.cs, build/code/IBuilder.cs, type/list/view, spec/render, kind/reflection,
  kind/list, step/actions/action) to the views; delete the three; verify teaching parity + baseline.

## Risks / open questions

- **`action` reflection ownership:** the plan says "reflection once at the `action` view leaf" —
  `action.Properties` is the ONLY reflection site. Confirm no consumer forces earlier eager walk.
- **`module`/`action` naming vs OBP:** `module.Actions` is a collection property (fine — the
  collection IS the API); `action.Properties` likewise. No verb+noun. `module` value vs the
  `app.Module` registry: the registry becomes the `list<module>` over time (Stage-5-adjacent per
  plan §"raw name maps dissolve").
- **Big-bang vs incremental:** 4a–4c can land + test behind the existing `Describe()` (both live),
  then 4d–4e switch and delete. Prefer that — keeps the suite green through the migration.

## Status

Plan written. Starting 4a (the `module` value + `app.module.list` collection), mirroring
`goal.list`. This is a large stage; landing incrementally (4a→4e), each piece built + tested,
baseline held at ~224.
