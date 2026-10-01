# v1 — Understand the new builder & refresh the builder docs

## Task

Ingi pulled `app-systems` and asked me to get familiar with the new builder (it changed
a lot) and refresh the docs, which had drifted. Full pass across all stale builder docs.

## What the builder actually is now (verified from source)

The builder was rearchitected from a **two-phase Plan→Compile** pipeline into a
**three-stage Decide→Properties→match** pipeline. The old pipeline is gone entirely
(`Plan.llm`, `Compile.llm`, `CompileUser.llm`, `BuildStep/`, `LlmFixer`, `builder.actions`,
`builder.validate`, `QueryAndVerify` — none exist on `app-systems`).

Pipeline (`os/system/builder/BuildGoal/Start.goal` → `Compile`):
1. **Decide** (`BuildGoal/Decide.goal`) — two exchanges with a generic `llm.decider`
   (State + typed Questions). Stage 1: per step, one module `choice` + a yes/no per
   **common action** (`decider.json`). Stage 2: only what stage 1 left open (the step's
   `Pick`). `build.pick` writes `step.Pick`.
2. **Properties** (`BuildGoal/Properties.goal`) — ONE `llm.query` for the whole goal; the
   LLM writes each step's actions in **formal** (`[i] module.action(Name=value); …`).
   System prompt = `llm/Properties.llm`; user msg = `templates/properties.template`.
3. **match** — `build.match` reads the formal lines into each step's `code`. Recoverable
   errors: `UnwrittenNumber`→`ConfirmNumbers` (decider), `ElseWithoutIf`→`SourceError`
   (programmer's bug, `SourceFix.llm`), other→`FixSteps` (re-ask refused steps only).
Then `build.fold` re-parents indented sub-steps into the preceding condition's `child`.

C# side: `build.{load,goals,pick,match,fold,goalsSave,unreached,appSave}` →
`PLang/app/module/build/code/Default.cs` (IBuilder). `llm.decider`→`llm/decider.cs` +
`IDecider`/`TypeSafe.cs` (posts to the "systemone" endpoint). `step.Pick` type at
`PLang/app/goal/step/pick/list/this.cs`. Catalog from `app.module.list` / `module.Describe`.

The `.pr` format also changed: `step[]` (not `steps`), `code[]` (not `actions`),
`property[]` (not `parameters`) with typed `type:{name,kind?,template?}` and parsed
`variable:[]`; a condition's body nests as `code[].child[]`; sub-goals as top-level `child[]`.
No `formal`, no `modifiers`, no `description`/`builderVersion` on the step/goal.

`examples.md` format changed: `Step text:` / `Properties:` JSON object (was `Mapping:` + `|`).

## Doc location decision (Ingi)

Co-locate as `.code.md` next to the code; keep CLI + cross-cutting in `Documentation/`.
Plus a `start.md` index in `os/system/builder/` guiding to all the others.

| Old (Documentation/v0.2) | New home |
|---|---|
| understanding-the-builder.md | os/system/builder/builder.code.md |
| building-the-builder.md | os/system/builder/bootstrap.code.md |
| debugging-builder-failures.md | os/system/builder/debugging.code.md |
| build_process.md | os/system/builder/pr-format.code.md |
| action-catalog.md | os/system/modules/catalog.code.md |
| build.md | stays in Documentation/v0.2 (fix pipeline lines only) |
| — | os/system/builder/start.md (NEW: index/guide) |

Reserved-stem note: `ScanOrphans` warns on `*.{notes,examples,description}.md` under module
folders; `.code.md` and `start.md` sidestep it (Decide.code.md already proves the pattern).

## Steps

1. Write `builder.code.md` (keystone overview) — locks terminology.
2. Write `start.md` (index).
3. Write `pr-format.code.md` (exact new .pr format, verified via jq).
4. Write `bootstrap.code.md`, `debugging.code.md`, `catalog.code.md`.
5. Fix `Documentation/v0.2/build.md` pipeline description (lines ~40-41, 95-96, 118).
6. `git rm` the 5 old Documentation/v0.2 docs; update inbound cross-links across
   Documentation/ that point to the moved files.
7. summary.md; commit (incl .bot/); push.

## Not doing

- Not touching the builder's runtime behavior or C# (understand-and-document task only).
- Not editing CLAUDE.md (docs-owned; propose via claude-md-proposals if needed).
