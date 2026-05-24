# security — compile-llm-notes-per-action

## Version
v1 — **PASS**

## What this is
Trust-boundary audit of `compile-llm-notes-per-action`. The branch
moves per-action LLM teaching from C# `[Description]`/`[Example]`
attributes into repo-tracked markdown files under
`os/system/modules/<module>/{module,<action>}.{description,notes,examples}.md`,
read at build time by a new loader (`PLang/app/modules/MarkdownTeaching.cs`)
and merged into the catalog the planner LLM sees.

## What was done

### v1 (this version, PASS)

Audited the seven-commit branch delta past the runtime2 merge
(`d8c18e257..55da8f529`). Production C# net additions: three files
(`MarkdownTeaching.cs`, `app/modules/this.cs`, `app/goals/.../action/this.cs`)
plus mechanical attribute deletions across ~130 handler files. Built
on tester v2's clean-rebuild baseline (C# 2945/2945, plang 204/204 +
drift 2/2 across 3 fresh-cache rounds).

### Findings

**None — 0 new critical/high/medium/low.**

The new surface is a read-only filesystem loader:

- `Load(modulesRoot, moduleName, actionName)` — only call site is
  `app/modules/this.cs:393` inside `Describe()`. `moduleName`/`actionName`
  come from C# reflection over compile-time-registered handler types;
  `modulesRoot` resolves from process-rooted `App.OsDirectory`. None
  reachable from a PLang program, HTTP origin, or deserialized payload.
- `ScanOrphans` enumerates a fixed directory tree and yields paths to
  the developer's `Output` channel — same trust level as the build
  command they just ran.
- Content sink is the LLM compile prompt (excluded per ruleset rule
  14) and developer terminal output.

The ~130 attribute deletions are documentation prose only — no
gating, no behavior, no auth/signing/permission surface changed. Spot-
checked on `output/*`, `signing/*`, `settings/*`, `mock/action`.

### Carry-forwards
F1/F2/F4 from `filesystem-permission` and F1/F2/F4 from
`path-polymorphism` remain open on `main` (auditor PASS on
`path-polymorphism` noted them as expected). Untouched on this
branch — relevant files (`actor/permission/this.cs`,
`types/path/permission/this.cs`) appear nowhere in the C# diff.

## Verdict
**PASS — ship.**

## Deliverables
- `.bot/compile-llm-notes-per-action/security/v1/plan.md`
- `.bot/compile-llm-notes-per-action/security/v1/result.md`
- `.bot/compile-llm-notes-per-action/security/v1/verdict.json`
- `.bot/compile-llm-notes-per-action/security-report.json`
