# Docs v1 Plan — UI Module (Template Rendering)

## Context

The coder implemented a UI module for Liquid template rendering via Fluid 2.31.0. Auditor passed, security passed (accepted risks), tester passed. I'm the final gate before merge.

## Gaps Identified

### 1. User-facing module docs — MISSING
No `docs/modules/ui.md` exists. PLang users need to know how to write template rendering steps.

### 2. Module index — MISSING entry
`docs/modules/index.md` has no UI module entry. Needs to be added.

### 3. Architecture docs — modules.md UPDATE
`Documentation/App/modules.md` Built-in Action Handlers table doesn't include the `ui` module.

### 4. Architecture docs — good_to_know.md UPDATE
The `ITemplateProvider` is registered in `Engine.Providers` with type mapping `"template"`. This should be documented alongside the other provider interfaces.

### 5. XML doc comments — REVIEW
The source files already have XML docs. I'll verify completeness.

## Plan

1. **Create `docs/modules/ui.md`** — user-facing docs with PLang examples, parameter tables, and security notes (SSTI warning per security report finding #1)
2. **Update `docs/modules/index.md`** — add UI module to the I/O section
3. **Update `Documentation/App/modules.md`** — add `ui | render` to the Built-in Action Handlers table
4. **Update `Documentation/App/good_to_know.md`** — add `ITemplateProvider` to the provider interfaces list
5. **Verify XML docs** on all new public members
6. **Write verdict.json and docs-report.json**
