# coder v1 — module-owns-action core, then the Validate trilogy

**Branch:** `goal-graph-singular`
**Driver:** architect `validate-trilogy-answer.md` (2026-07-28) — Q1 escalated the Validate trilogy:
do NOT build `Validate` against `action.Module`/`ActionName` strings; `module-owns-action.md` is
RELEASED as the prerequisite. Pipeline: **module-owns-action core → Validate trilogy** → backref-pass.

Q2 settled the split I proposed: **if it changes the action it is construction (builder's job); if it
only judges the action it is `Validate` (the node's job).**

## Baseline

Captured this session by stash+rebuild+full-suite, failure names diffed (`/tmp/b2_<suite>.txt`):
branch is broadly red **pre-existing** (mid-refactor). My store-filter commit `2ab950333` added
**zero** new failures. Any test that flips red in this work is mine.

Prereq verifications from the doc, run before planning:
1. `item.@this` base does **not** declare `Name` → the property name is free on `action.@this`. ✓
2. Wire key is already `"name"` (verified from a live Store embed: `{"module":"output","name":"write"}`)
   — the C# rename `ActionName → Name` *aligns* C# with the wire rather than changing it. ✓

## Stages

Each stage builds green and commits separately. Rename and type-change are split deliberately: they
touch overlapping files but different symbols, and a green checkpoint between them means a regression
is attributable.

### Stage A — `ActionName → Name`, qualified face → `ToString()`
- `action.@this`: `ActionName` → `Name` (keeps `[Store, LlmBuilder, Debug, Default]`, wire key `"name"`).
- Delete the computed `Name => $"{Module}.{ActionName}"`; becomes
  `ToString() => _module is null ? Name : $"{_module.Name}.{Name}"` (never throws).
- All `$"{a.Module}.{a.ActionName}"` sites → `$"{a}"`.
- Templates (language boundary): `{{ a.ActionName }}` → `{{ a.Name }}` in `actionFormal.template`,
  `stepForLlm.template`, `os/system/actions/summary.md`, `os/system/actions/v2/summary.md`.
  `{{ a.Module }}` unchanged (element `ToString()` keeps rendering `file`).
- Scope: ~76 prod C# sites, ~214 test sites, 3 template files.

### Stage B — `action.Module : string → module.@this` (the element)
- Private `_module` + throwing getter (never nullable); `[Debug]` only — no `[Store]`.
- `module.@this.Name` gets `[Debug, Out]`; `module.@this.ToString() => Name`.
- The `Create` chain (one vocabulary word; **each owner reads exactly the one dict key it owns**):
  static `action.Create(raw, data)` (shape-check + delegate, zero `d.Get`) → `module.list.Create(dict,data)`
  reads `"module"` → `module.Create(dict,data)` reads `"name"` → catalog `Create()` / `Create(dict,data)`.
  `modifier.@this` overrides `Create()` covariantly (adds Position).
- `module.Mint()` → private `Create()`.
- Dispatch: `Module.Create(Name, context)`; public `list.GetCodeGenerated(action, ctx)` dies →
  internal `Handler(module, name, ctx)`.
- pr reader (`action/serializer/Reader.cs`): stop pre-creating (`new action.@this()` dies), stop
  deciding role by array position; read the two leading identity keys, ask the catalog for the blank,
  pull-fill the rest. **Wire key order (`module`,`name` first) becomes a contract** — violation /
  unknown module / unknown action throws with a rebuild message. Modifiers get `Position` back from
  the registry. Reconcile with my landed `44bdb11d6` key-alignment work — do not redo it.
- Site sweep per the doc's table (Nest, goalEntryAction, Schema partial, LifecycleFor, Snapshot,
  CallChainRenderer, Default.cs).

### Stage C — consumers' questions become action members
- `action.Capabilities` (owns `RequiresCapabilityAttribute` reflection) ← `discover.cs:257-260`.
- Action build-validate member ← `Default.cs:648-654` inline `IBuildValidatable` reflection.
- `getTypes.DetermineReturnType` dies → `action.Return` (reaches App via `Module`).
- No `Handler` property on action (middleman — explicitly rejected by the doc).

### Stage D — the Validate trilogy (the actual item 7)
```csharp
goal.Validate(context) → Step.Validate → action.list.Validate → action.Validate   // mirrors Run/Output
```
`action.Validate(context)` — pure verdicts, diagnostics onto its own `Warning`:
- catalog → `Module[Name] != null` (the **held element** — no registry strings)
- required → the catalog element's rows (non-null, no `[Default]`, not emitted)
- validatable → its own build-validate member (Stage C — no fresh reflection)
- goalcall → CLR-name reject (`Default.cs:583`) / dotted-name reject (`:612`, the non-repair branch)

**Stays builder-side (construction, per Q2):** `:557` `a.Default = GetDefaults(...)`; `:600-611`
goal.call name-repair (`SetValue` + warn). `NormalizeParameterTypes` stays put (normalize, pre-validate).
`BuildResponse.Validate` bridges to recovery as already ruled.
Builder end-state: fold/normalize/repair → `goal.Validate(context)` → react (FixValidation / abort).

## Discipline

- Production C# and all `.goal` edits go through Edit/Write **per file** — never `sed`. Only
  `PLang.Tests/` C# may be shell-batched.
- Build once per batch of edits, then run the targeted suite; full suite + failure-name diff vs the
  captured baseline before each commit.
- Read the `.pr` after any rebuild (builder is non-deterministic).

## Open caveat (not blocking, carried)

`%!app.type.list%` rendering in the compile prompt is still unverified — a debug watch showed
`(undefined)`, but this branch's debug-arg binding is itself broken (three separate lower-door bind
failures observed: `files` string→list, `variables` string→DebugVariable, `step` Int64→Nullable<Int32>).
Needs checking via the actually-rendered `CompileUser` output, not the watcher. Flagged, not chased.
