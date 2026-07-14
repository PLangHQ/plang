# module-discovery branch — Stage 4 (split out of navigation-driven-record-builder)

This branch carries Stage 4 (**module-discovery**) of the record-builder plan, split off so it
gets its own architect plan and clean baseline.

## Lineage
- Branched off `navigation-driven-record-builder` @ `e7e38861b` — so it carries **all of stages
  1–3** (create-unification / STJ-collapse / born-native-lift defork) **plus the stabilization
  pass** (361 → 195 reds, zero regressions). See that branch's
  `.bot/navigation-driven-record-builder/coder/summary.md` for the full history.
- **Baseline to hold: ~195 reds** (the stabilization end state). Stage 4 must not increase it;
  remaining reds there are characterized in `navigation-driven-record-builder`'s
  `coder/stabilization-remaining.md` (scattered assertions + deferrals) and are not Stage-4 work.

## What Stage 4 is
Replace `module.Describe()` (imperative reflection → `StepActions`) with **value-object views**
(`app.module.list : list<module>` → `module.Actions : list<action>` → `action.Properties :
list<type>`) + Fluid `ui.render` templates; delete `Describe`/`StepActions`/`BuildTypeEntries`.
Includes **4f — the test report via `ui.render`** (retire the bespoke `test/report.cs` +
`test/junit/this.cs` serializers; render `list<test-result>` through a template).

## Coder's seed
`stage4-plan-seed.md` is my decomposition (4a–4f) from the record-builder plan §"Stage 4" — a
STARTING point, not the plan. The `module`/`action` value types are a new core-type surface that
wants an architect shape-pass (as `type.list` got `stage3-type-collection-answer.md`).

## Next
**Architect to author `.bot/module-discovery/architect/plan.md`.** Then coder implements 4a→4f
incrementally, each piece built + tested, baseline held at ~195.
