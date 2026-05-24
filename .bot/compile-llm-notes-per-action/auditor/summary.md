# Auditor — compile-llm-notes-per-action

## Version
v1 — PASS — see `v1/result.md`, `v1/verdict.json`, `../auditor-report.json`.

## What this is
Cross-cutting integrity audit of `compile-llm-notes-per-action`. The
branch moves per-action LLM teaching out of the `Compile.llm` system
prompt (and out of C# `[Description]`/`[Example]` attributes) into
markdown files at `os/system/modules/<module>/{module,<action>}.{notes,
examples,description}.md`, with the renderer surfacing each action's
prose only when the planner picked the action. Opportunistic
`[Provider]` → `[Code]` rename rides along.

Pipeline before me: architect v1 → test-designer v1 → coder v1..v3 →
tester v1 (NEEDS-FIXES F1: drift `.pr` Stale) → tester v2 (PASS, 3
fresh-cache rounds) → security v1 (PASS, 0 new findings).

## Verdict
**v1 — PASS.** Both architect verification checks reproduce on an
independent clean rebuild (Compile.llm = 14905 bytes < 16 KB; drift
cases 2/2 across 3 fresh-cache rounds). C# suite 2945/2945. All 8
architect stages delivered; two new low-severity nits filed below.

## New findings — both low / informational

**F1 — orphan-scan never fires in production.** `MarkdownTeaching.
ScanOrphans` + `Modules.WarnOrphansAsync` exist and are tested, but
nothing in `PLang/` or `PlangConsole/` invokes the warn — only test
code does. Today there are no orphans (every stem maps to a registered
action), so observable impact is zero. The risk is latent: the next
typo'd or stale teaching file will not produce the warning the
architect's Stage 7 promised. Five-line fix (wire into builder
startup or first `Describe()` call). Missed because tester verified
the function works when called and security verified the mechanism is
safe — neither asked "is it called?"

**F2 — RETRACTED on rebase.** Initially filed re: CLAUDE.md `[Provider]`
references. Docs commit `65538f9dc` landed concurrently and closed it
(CLAUDE.md lines 39 + 43 now read `[Code]` / `Emission/Property/
{Data,Code}/`). Auditor process gap — didn't `git fetch` before
reporting. Kept in the record rather than silently dropped.

## Carried (unchanged at HEAD)
- HttpPath.Raw userinfo (latent F1 from path-polymorphism auditor v1)
- F1/F2/F4 from filesystem-permission
- O5/O6 from path-polymorphism security v3

## Confidence
High on the stage delivery (traced each architect stage to disk) and on
the verification reproduction (drove the builds myself). The two new
findings are the seam-between-axes class — non-blocking, but filed so
they're adjudicated rather than inherited.
