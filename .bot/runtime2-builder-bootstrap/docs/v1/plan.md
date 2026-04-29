# Docs v1 Plan — runtime2-builder-bootstrap

## Context

Pre-merge docs gate after auditor PASS (3 minor + 2 nit), security PASS (2 medium info-disclosure + 4 low), tester APPROVED (2309/2309 C# green; F4 carryover bucketed for follow-up branch), codeanalyzer v4 CLEAN.

Coder's stated goal was three small gaps to make the v2 builder self-host:
1. `variable.set` adds `AsDefault` (set only when unset).
2. `file.read` adds `ResolveVariables` (resolve `%var%` patterns in content; security improvement = `skipInfrastructure: true` blocks `%!app%`).
3. `TypeMapping.ConvertTo` auto-wraps single value → `List<T>`.

Real branch is bigger — full v2 self-rebuild + Catalog + Trace + ParamSnapshot + granular LLM debug + `formal` Fluid filter + `validateResponse`/`enrichResponse`/`promoteGroups`/`merge` builder actions + `[PlangType]` shape teaching + math `ExamplesForLlm()` + `IsCatalogDescription` helper + BuildingGuard removal.

## What's already documented on this branch

The branch ships meaningful doc work:

- `Documentation/v0.2/action-catalog.md` (234 lines, NEW) — full Catalog spec with attribute table, type rendering, example syntax, end-to-end annotated `variable.set`. Strong.
- `Documentation/v0.2/trace.md` (95 lines, NEW) — Trace identity, ownership, file layout, why-on-Context.
- `Documentation/v0.2/debug.md` (76 lines added) — granular `llm` sub-flags, `Debug.Write` channel, `pass1.response` post-pipeline caveat.
- `Documentation/v0.2/good_to_know.md` (7 lines added) — math `ExamplesForLlm()` rule + `Variables.Snapshot` exclusions.
- XML docs are present and meaningful on every new class I checked: `validateResponse`, `enrichResponse`, `promoteGroups`, `ExampleSpec`, `ActionSpec`, `ExampleRenderer`, `ParamSnapshot`, `variable.set`, `file.read`.

So the dev-facing surface is largely covered. The gaps are user-facing + a couple of stale references.

## Gaps I'll fill

### G1 (user-facing, major) — `docs/modules/variable.md` missing `AsDefault`

The `set` action's parameter table lists `Name | Value | Type` only. The new `AsDefault` parameter — semantically meaningful (matches PLang's `set default %x% = …` natural form) — is invisible to users. Add the parameter row + a brief example.

### G2 (user-facing, major) — `docs/modules/file.md` missing `ResolveVariables`

The `read` action's parameter table lists `Path` only. The new `ResolveVariables` parameter, plus the security note that `%!app%`/`%!fileSystem%` etc. are blocked when reading file contents, has no user-facing teaching. Add the parameter row + a one-paragraph note.

### G3 (user-facing, major) — `docs/modules/builder.md` stale BuildingGuard section

Line 177-180 says "All builder actions check `engine.Building.IsEnabled` before executing." That guard was deliberately removed (commit 4633674c). Update or remove the section, and clarify the current posture: any signed `.pr` can call builder actions; the trust boundary is the goal signature.

The doc also doesn't list the new actions (`validateResponse`, `enrichResponse`, `promoteGroups`, `merge`). They are build-pipeline-only and the doc already states "If you're writing PLang applications, you don't need this module." I'll add a brief table entry for completeness so future readers don't think they're missing — the doc is the only place users would look.

### G4 (dev-facing, minor) — `Documentation/v0.2/good_to_know.md` stale BuildingGuard

Line 424 still describes `BuildingGuard(IContext)` as the guard called first in every provider method. Remove or rewrite to match current state — the static method is gone; what's left is `App.Build.IsEnabled` checked at the file-provider read path (`DefaultFileProvider`), with no per-action guard on the write path. Mirror the language the auditor F1 finding settled on.

### G5 (dev-facing, minor) — Catalog + Trace not listed in good_to_know.md or architecture.md

Neither doc points readers at the new `action-catalog.md` and `trace.md` files. A future contributor finding `App.Catalog` or `Context.Trace` in code won't know dedicated docs exist. Add cross-references — either a "see also" entry in `good_to_know.md`'s Modules section, or a short pointer paragraph in `architecture.md`'s Modules + Context sections.

### G6 (dev-facing, minor) — `merge.cs` XML doc

`PLang/App/modules/builder/merge.cs` has only a `[Description("...")]` attribute and no `///` summary. Every other new builder action class on this branch has both. Add a one-line summary describing it is build-pipeline-only and what it merges (LLM-generated step → existing step, preserving runtime fields).

## What I'm NOT doing

- **Not writing PLang `.goal` examples.** Per character file, `.goal` examples are the tester's job. The `validateResponse`/`enrichResponse`/`promoteGroups` actions are also build-pipeline-only — no user-callable example to write.
- **Not writing a CHANGELOG entry.** I checked the repo: no CHANGELOG.md exists. Creating one for a single branch is over-scope; raise as a separate decision if the team wants one.
- **Not addressing security findings.** F1 (`ParamSnapshot` bypasses `[Sensitive]`) and F2 (Variables.Snapshot leak) are open coder follow-ups, not docs gaps. Same for auditor F2 (Step.Clone) and F5 (Debug.Apply test). None of these say "docs are wrong" — they say "code/test should change."
- **Not touching old v1 docs in `Documentation/modules/`.** Per memory: those are deprecated; ignore.

## Verdict if all gaps fill

Pass → branch is mergeable. The originally documented gaps (1+2+3) are user-facing teaching, not code clarity, so filling them shouldn't change verdict to fail.

If during writing I discover an XML doc claim that contradicts behaviour or a reference that points at a moved file, I'll either fix it (XML doc, easy) or fail and route to coder (intent unclear).

## Order of work

1. Fill G6 (smallest — single XML summary).
2. Fill G1, G2, G3 (user-facing — biggest reader benefit).
3. Fill G4 (stale ref correction).
4. Fill G5 (cross-references).
5. Verify by re-reading each touched file end-to-end.
6. Write `docs-report.json`, `verdict.json`, `summary.md`, update `docs/summary.md`, commit.

## Risk

Low. All gaps are additive or correct-an-error. No new conceptual content the team hasn't already debated — the existing branch docs already explain Catalog, Trace, granular LLM debug. I'm closing user-doc gaps and stale references, not authoring an architecture position.
