# Docs v1 Plan — runtime2-builder-plan

## Context

Coder v7 done (security fixes), auditor v1 PASS (6 findings, 1 major fixed), tester v8 APPROVED (2085/2086), security v2 PASS. This is the first docs pass. The branch has 4 major areas of change:

1. **Return removal** — `action.Return` replaced by `%__data__%` + `variable.set`
2. **Condition orchestration** — `condition.if` now handles if/elseif/else via multiple actions in one step, sets `step.Disabled`
3. **Data.Compare** — new structural comparison for eval testing
4. **Security hardening** — HTTP download limits, SSE overflow, slow-loris, Ed25519 timing, JSON depth/count guards, ResolveDeep breadth guard

## Gaps Found

### Architecture Docs (STALE)
1. **goals-steps.md** — Still documents `Return` property as primary mechanism. Missing `%__data__%` explanation.
2. **execution-flow.md** — Return variable mapping description is stale. Missing condition orchestration flow.
3. **architecture.md** — `action.Return` references stale. Missing `%__data__%` data flow.
4. **good_to_know.md** — Missing condition orchestration pattern (step.Disabled, __condition_orchestrating__ guard).
5. **variables.md** — Missing `%__data__%` as reserved variable.

### XML Doc Comments (GAPS)
6. **JsonStringNavigator.cs** — `MaxElementCount` and `MaxDepth` constants lack XML docs.
7. **Data.Clone()** — Missing comment explaining why events are intentionally not copied (auditor finding #6).
8. **Variables.ResolveDeep** — Breadth guard not documented in XML.

### Missing Docs
9. **Data.Compare** — No architecture doc explaining the compare facility.
10. **Security hardening** — No summary doc of what was hardened and why.

## Plan

### Phase 1: Update stale architecture docs
- Update `goals-steps.md`: document `%__data__%` mechanism, mark Return as legacy
- Update `execution-flow.md`: fix return flow, add condition orchestration section
- Update `architecture.md`: fix action.Return references
- Update `good_to_know.md`: add condition orchestration pattern, step.Disabled explanation
- Update `variables.md`: add `%__data__%` as reserved variable

### Phase 2: Add XML doc comments
- `JsonStringNavigator.cs`: docs on MaxElementCount, MaxDepth
- `Data/this.cs`: comment on Clone() re: events not copied
- `Variables/this.cs`: document breadth guard in ResolveDeep

### Phase 3: Write new docs
- Add Data.Compare section to appropriate doc file
- Add security hardening summary to good_to_know.md or separate doc

### Phase 4: Reports
- Write docs-report.json, verdict.json, summary.md
- Update report.json, commit and push
