# Security Analysis v1 — UI Module (Template Rendering)

## Scope

Branch `runtime2-builder-v2-template` adds:
- **UI module**: `render.cs`, `FluidProvider.cs`, `ITemplateProvider.cs` — Liquid template rendering via Fluid 2.31.0
- **module.remove**: Unregister a module by name
- **event.on / event.skipAction**: Event binding registration and action skipping
- **Properties.cs**: New collection class for Data properties
- **IdentityData**: Clone override fix (type slicing prevention)
- **DefaultEvaluator**: `InvalidCastException` now caught (carry-forward fix)

## Analysis Plan

### Phase 1: Blue Team (Defensive Audit)
1. **FluidProvider attack surface** — SSTI, path traversal, callGoal execution, memory stack exposure, XSS, resource exhaustion
2. **PlangFileProvider** — include/render path resolution, sandbox enforcement via ValidatePath
3. **module.remove** — built-in module removal, authorization
4. **event.on** — regex patterns, goal execution from bindings
5. **Carry-forward verification** — check standing open findings from previous branches

### Phase 2: Red Team (Exploit Sketches)
- SSTI via callGoal tag when template content is user-supplied
- Path traversal via include tags
- Resource exhaustion via recursive includes or large templates

### Phase 3: Report
- Write `security-report.json`, `verdict.json`, `summary.md`
- Update carry-forward findings in memory
